using System.Diagnostics.CodeAnalysis;
using System.IO;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Logging;
using TrackyTrack.Data;
using Newtonsoft.Json;

namespace TrackyTrack;

[Serializable]
//From: https://github.com/MidoriKami/DailyDuty/
public class CharacterConfiguration
{
    // Increase with version bump
    public int Version { get; set; } = 1;

    public ulong LocalContentId;

    public string CharacterName = "";
    public string World = "Unknown";
    public Desynth Storage = new();
    public VentureCoffer Coffer = new();
    public GachaThreeZero GachaThreeZero = new();
    public GachaFourZero GachaFourZero = new();

    public CharacterConfiguration() { }

    public CharacterConfiguration(ulong id, PlayerCharacter local)
    {
        LocalContentId = id;
        CharacterName = Utils.ToStr(local.Name);
        World = Utils.ToStr(local.HomeWorld.GameData!.Name);
    }

    public void Save(bool saveBackup = false)
    {
        if (LocalContentId != 0)
        {
            // if (saveBackup)
            // {
            //     var org = GetConfigFileInfo(LocalContentId);
            //     var backup = GetBackupConfigFileInfo(LocalContentId);
            //     if (!backup.Exists)
            //         backup.Delete();
            //
            //     org.CopyTo(backup.FullName);
            // }


            SaveConfigFile(GetConfigFileInfo(LocalContentId));
        }
    }

    public void SaveBackup() => Save(true);
    private static FileInfo GetBackupConfigFileInfo(ulong contentID) => new(Plugin.PluginInterface.ConfigDirectory.FullName + $@"\{contentID}.bak.json");

    public static CharacterConfiguration Load(ulong contentId)
    {
        try
        {
            var mainConfigFileInfo = GetConfigFileInfo(contentId);

            return TryLoadConfiguration(mainConfigFileInfo);
        }
        catch (Exception e)
        {
            PluginLog.Warning(e, $"Exception Occured during loading Character {contentId}. Loading new default config instead.");
            return CreateNew();
        }
    }

    private static CharacterConfiguration TryLoadConfiguration(FileSystemInfo? mainConfigInfo = null)
    {
        try
        {
            if (TryLoadSpecificConfiguration(mainConfigInfo, out var mainCharacterConfiguration))
                return mainCharacterConfiguration;
        }
        catch (Exception e)
        {
            PluginLog.Warning(e, "Exception Occured loading Main Configuration");
        }

        return CreateNew();
    }

    private static bool TryLoadSpecificConfiguration(FileSystemInfo? fileInfo, [NotNullWhen(true)] out CharacterConfiguration? info)
    {
        if (fileInfo is null || !fileInfo.Exists)
        {
            info = null;
            return false;
        }

        info = JsonConvert.DeserializeObject<CharacterConfiguration>(LoadFile(fileInfo));
        return info is not null;
    }

    private static FileInfo GetConfigFileInfo(ulong contentId) => new(Plugin.PluginInterface.ConfigDirectory.FullName + @$"\{contentId}.json");

    private static string LoadFile(FileSystemInfo fileInfo)
    {
        using var reader = new StreamReader(fileInfo.FullName);
        return reader.ReadToEnd();
    }

    private static void SaveFile(FileSystemInfo file, string fileText)
    {
        using var writer = new StreamWriter(file.FullName);
        writer.Write(fileText);
    }

    private void SaveConfigFile(FileSystemInfo file)
    {
        var text = JsonConvert.SerializeObject(this, Formatting.Indented);
        SaveFile(file, text);
    }

    public static CharacterConfiguration CreateNew() => new()
    {
        LocalContentId = Plugin.ClientState.LocalContentId,

        CharacterName = Plugin.ClientState.LocalPlayer?.Name.ToString() ?? "",
        World = Plugin.ClientState.LocalPlayer?.HomeWorld.GameData?.Name.ToString() ?? "Unknown"
    };
}
