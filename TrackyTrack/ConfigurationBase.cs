using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using TrackyTrack.Data;

namespace TrackyTrack;

// Based on: https://github.com/Penumbra-Sync/client/blob/main/MareSynchronos/MareConfiguration/ConfigurationServiceBase.cs
public class ConfigurationBase : IDisposable
{
    private record SaveObject(ulong ContentId, string FilePath, CharacterConfiguration Character);

    private readonly Plugin Plugin;

    private readonly CancellationTokenSource CancellationToken = new();
    private readonly ConcurrentDictionary<ulong, DateTime> LastWriteTimes = new();
    private readonly ConcurrentQueue<SaveObject> SaveQueue = new();

    public string ConfigurationDirectory { get; init; }
    private string MiscFolder { get; init; }

    public ConfigurationBase(Plugin plugin)
    {
        Plugin = plugin;

        ConfigurationDirectory = Plugin.PluginInterface.ConfigDirectory.FullName;
        MiscFolder = Path.Combine(ConfigurationDirectory, "Misc");
        Directory.CreateDirectory(MiscFolder);
    }

    public void StartTasks()
    {
        Task.Run(CheckForConfigChanges, CancellationToken.Token);
        Task.Run(SaveAndTryMoveConfig, CancellationToken.Token);
    }

    public void Dispose()
    {
        CancellationToken.Cancel();
        CancellationToken.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Load()
    {
        foreach (var file in Plugin.PluginInterface.ConfigDirectory.EnumerateFiles())
            if (ulong.TryParse(Path.GetFileNameWithoutExtension(file.Name), out var id))
                Plugin.CharacterStorage[id] = LoadConfig(id);
    }

    private string LoadFile(FileSystemInfo fileInfo)
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                using var reader = new StreamReader(fileInfo.FullName);
                return reader.ReadToEnd();
            }
            catch
            {
                if (i == 4)
                    Utils.AddNotification("Failed to read config", NotificationType.Error);

                Plugin.Log.Warning($"Config file read failed {i + 1}/5");
            }
        }

        return string.Empty;
    }

    public CharacterConfiguration LoadConfig(ulong contentId)
    {
        CharacterConfiguration? config;
        try
        {
            var file = new FileInfo(Path.Combine(ConfigurationDirectory, $"{contentId}.json"));
            config = JsonConvert.DeserializeObject<CharacterConfiguration>(LoadFile(file));
            if (config == null)
                Plugin.Log.Error($"Found invalid json file: {contentId}.json");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Exception Occured during loading Character {contentId}. Loading new default config instead.");
            Utils.AddNotification("Exception during config load, pls check /xllog", NotificationType.Error);
            config = CharacterConfiguration.CreateNew();
        }

        config ??= CharacterConfiguration.CreateNew();


        if (config.Version < 3)
        {
            config.Version = 3;

            // Wipe the fragment overflow from older versions
            foreach (var (key, dict) in config.Lockbox.History)
                if (Lockboxes.Fragments.Contains((uint)key))
                {
                    config.Lockbox.Opened -= dict.Count;
                    dict.Clear();
                }

            Save(contentId, config);
        }

        if (config.Version < 4)
        {
            config.Version = 4;

            var lowestValidID = 100;
            var highestValidID = Plugin.Data.GetExcelSheet<Item>()!.Where(i => i.Icon != 0).MaxBy(i => i.RowId)!.RowId;

            // Wipe the invalid desynth data from older versions
            foreach (var (key, desynthResult) in config.Storage.History.ToArray())
            {
                if (desynthResult.Source > highestValidID || desynthResult.Source < lowestValidID)
                    config.Storage.History.Remove(key);

                if (desynthResult.Received.Length == 0)
                {
                    config.Storage.History.Remove(key);
                    continue;
                }

                var itemResult = desynthResult.Received.First();
                if (itemResult.Item > highestValidID || itemResult.Item < lowestValidID)
                {
                    config.Storage.History.Remove(key);
                    config.Storage.Total.Remove(itemResult.Item);
                }
            }

            // Wipe the fragment inspection hiccup from older versions
            var invalidItems = new uint[] { 31664, 31326, 33672, 32828, 33706, 33257 };
            foreach (var (key, dict) in config.Lockbox.History)
            {
                if (!Lockboxes.Fragments.Contains((uint) key))
                    continue;

                foreach (var itemId in dict.Keys.Where(i => invalidItems.Contains(i)).ToArray())
                    dict.Remove(itemId);
            }

            Save(contentId, config);
        }

        LastWriteTimes[contentId] = GetConfigLastWriteTime(contentId);
        return config;
    }

    public void SaveCharacterConfig()
    {
        // Only allow saving of current character
        var contentId = Plugin.PlayerState.ContentId;
        if (contentId == 0)
        {
            Plugin.Log.Error("ClientId was 0 but something called Save()");
            return;
        }

        if (!Plugin.CharacterStorage.TryGetValue(contentId, out var savedConfig))
            return;

        Save(contentId, savedConfig);
    }

    public void SaveAll()
    {
        // This saves all characters, only allow calls if one process is running
        if (Process.GetProcessesByName("ffxiv_dx11").Length > 1)
            return;

        foreach (var (contentId, savedConfig) in Plugin.CharacterStorage)
            Save(contentId, savedConfig);
    }

    private void Save(ulong contentId, CharacterConfiguration savedConfig)
    {
        var filePath = Path.Combine(ConfigurationDirectory, $"{contentId}.json");
        try
        {
            var existingConfigs = Directory.EnumerateFiles(MiscFolder, $"{contentId}.json.bak.*").Select(c => new FileInfo(c)).OrderByDescending(c => c.LastWriteTime);
            foreach (var file in existingConfigs.Skip(5))
                file.Delete();

            File.Copy(filePath, $"{Path.Combine(MiscFolder, $"{contentId}.json")}.bak.{DateTime.Now:yyyyMMddHH}", overwrite: true);
        }
        catch
        {
            // ignore if file backup couldn't be created once
        }

        SaveQueue.Enqueue(new SaveObject(contentId, filePath, savedConfig));
    }

    public void DeleteCharacter(ulong id)
    {
        if (!Plugin.CharacterStorage.ContainsKey(id))
            return;

        try
        {
            LastWriteTimes.TryRemove(id, out _);
            Plugin.CharacterStorage.Remove(id, out _);
            var file = new FileInfo(Path.Combine(ConfigurationDirectory, $"{id}.json"));
            if (file.Exists)
                file.Delete();
        }
        catch (Exception e)
        {
            Plugin.Log.Error("Error while deleting character save file.");
            Plugin.Log.Error(e.Message);
        }
    }

    private async Task SaveAndTryMoveConfig()
    {
        while (!CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.Token);

            if (!SaveQueue.TryDequeue(out var queueObject))
                continue;

            try
            {
                var tmpPath = $"{Path.Combine(MiscFolder, $"{queueObject.ContentId}.json.tmp")}";
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);

                File.WriteAllText(tmpPath, JsonConvert.SerializeObject(queueObject.Character, Formatting.Indented));
                for (var i = 0; i < 5; i++)
                {
                    try
                    {
                        File.Move(tmpPath, queueObject.FilePath, true);
                        LastWriteTimes[queueObject.ContentId] = new FileInfo(queueObject.FilePath).LastWriteTimeUtc;
                        break;
                    }
                    catch
                    {
                        // Just try again until counter runs out
                        if (i == 4)
                            Utils.AddNotification("Failed to move config", NotificationType.Warning);

                        Plugin.Log.Warning($"Config file couldn't be moved {i + 1}/5");
                        await Task.Delay(30, CancellationToken.Token);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e.Message);
                Plugin.Log.Error(e.StackTrace ?? "Null Stacktrace");
            }
        }
    }

    private async Task CheckForConfigChanges()
    {
        while (!CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.Token);

            foreach (var file in Plugin.PluginInterface.ConfigDirectory.EnumerateFiles())
            {
                if (ulong.TryParse(Path.GetFileNameWithoutExtension(file.Name), out var id))
                {
                    // No need to override current character as we already have up to date config
                    if (id == Plugin.PlayerState.ContentId)
                        continue;

                    var lastWriteTime = GetConfigLastWriteTime(id);
                    if (lastWriteTime != LastWriteTimes.GetOrCreate(id))
                    {
                        LastWriteTimes[id] = lastWriteTime;
                        Plugin.CharacterStorage[id] = LoadConfig(id);
                    }
                }
            }

            if (Plugin.SessionCopyState == SessionState.NotLoaded)
            {
                Plugin.SessionCopyState = SessionState.Loading;

                foreach (var (key, characterConfig) in Plugin.CharacterStorage)
                {
                    var config = JsonConvert.DeserializeObject<CharacterConfiguration>(JsonConvert.SerializeObject(characterConfig, Formatting.Indented));
                    Plugin.SessionCharacterCopy[key] = config ?? CharacterConfiguration.CreateNew();
                }

                Plugin.SessionCopyState = SessionState.Done;
            }
        }
    }

    private DateTime GetConfigLastWriteTime(ulong contentId) => new FileInfo(Path.Combine(ConfigurationDirectory, $"{contentId}.json")).LastWriteTimeUtc;
}
