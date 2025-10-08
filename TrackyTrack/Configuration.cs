using Dalamud.Configuration;

namespace TrackyTrack;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool EnableTeleport = true;
    public bool EnableRepair = true;
    public bool EnableCurrency = true;

    public bool EnableDesynthesis = true;
    public bool EnableBulkSupport = true;
    public bool EnableRetainer = true;
    public bool EnableVentureCoffers = true;
    public bool EnableGachaCoffers = true;
    public bool EnableEurekaCoffers = true;
    public bool EnableLockboxes = true;

    public bool ShowUnlockCheckmark = true;

    public bool UploadNotification = true;
    public DateTime UploadNotificationReceived = DateTime.MaxValue;
    public bool UploadPermission = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}