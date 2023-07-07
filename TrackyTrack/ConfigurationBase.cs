using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Logging;
using Newtonsoft.Json;

namespace TrackyTrack;

// Based on: https://github.com/Penumbra-Sync/client/blob/main/MareSynchronos/MareConfiguration/ConfigurationServiceBase.cs
public class ConfigurationBase : IDisposable
{
    private readonly Plugin Plugin;
    private readonly CancellationTokenSource CancellationToken = new();
    private readonly Dictionary<ulong, DateTime> LastWriteTimes = new();

    public string ConfigurationDirectory { get; init; }

    public ConfigurationBase(Plugin plugin)
    {
        Plugin = plugin;
        ConfigurationDirectory = Plugin.PluginInterface.ConfigDirectory.FullName;

        Task.Run(CheckForConfigChanges, CancellationToken.Token);
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
        {
            if (file.Name.Contains(".bak"))
                continue;

            ulong id;
            try
            {
                id = Convert.ToUInt64(Path.GetFileNameWithoutExtension(file.Name));
            }
            catch (Exception e)
            {
                PluginLog.Error($"Found file that isn't convertable. Filename: {file.Name}");
                PluginLog.Error(e.Message);
                continue;
            }

            var config = LoadConfig(id);
            Plugin.CharacterStorage[id] = config;
        }
    }

    private static string LoadFile(FileSystemInfo fileInfo)
    {
        using var reader = new StreamReader(fileInfo.FullName);
        return reader.ReadToEnd();
    }

    public CharacterConfiguration LoadConfig(ulong contentId)
    {
        CharacterConfiguration? config;
        try
        {
            var file = new FileInfo(Path.Combine(ConfigurationDirectory, $"{contentId}.json"));
            config = JsonConvert.DeserializeObject<CharacterConfiguration>(LoadFile(file));
        }
        catch (Exception e)
        {
            PluginLog.Warning(e, $"Exception Occured during loading Character {contentId}. Loading new default config instead.");
            config = CharacterConfiguration.CreateNew();
        }

        config ??= CharacterConfiguration.CreateNew();

        LastWriteTimes[contentId] = GetConfigLastWriteTime(contentId);
        return config;
    }

    public void SaveCharacterConfig()
    {
        // Only allow saving of current character
        var contentId = Plugin.ClientState.LocalContentId;
        if (contentId == 0)
            return;

        if (!Plugin.CharacterStorage.TryGetValue(contentId, out var savedConfig))
            return;

        var filePath = Path.Combine(ConfigurationDirectory, $"{contentId}.json");
        var existingConfigs = Directory.EnumerateFiles(ConfigurationDirectory, $"{contentId}.json.bak.*")
                                       .Select(c => new FileInfo(c)).OrderByDescending(c => c.LastWriteTime).ToList();
        if (existingConfigs.Skip(5).Any())
        {
            foreach (var file in existingConfigs.Skip(5).ToList())
            {
                file.Delete();
            }
        }

        try
        {
            File.Copy(filePath, $"{filePath}.bak.{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
        }
        catch
        {
            // ignore if file backup couldn't be created once
        }

        File.WriteAllText(filePath, JsonConvert.SerializeObject(savedConfig, Formatting.Indented));
        LastWriteTimes[contentId] = new FileInfo(filePath).LastWriteTimeUtc;
    }

    private async Task CheckForConfigChanges()
    {
        while (!CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.Token);

            foreach (var (contentId, savedWriteTime) in LastWriteTimes)
            {
                var lastWriteTime = GetConfigLastWriteTime(contentId);
                if (lastWriteTime != savedWriteTime)
                    Plugin.CharacterStorage[contentId] = LoadConfig(contentId);
            }
        }
    }

    private DateTime GetConfigLastWriteTime(ulong contentId) => new FileInfo(Path.Combine(ConfigurationDirectory, $"{contentId}.json")).LastWriteTimeUtc;
}
