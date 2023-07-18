using Dalamud.Configuration;
using Dalamud.Plugin;

namespace TrackyTrack
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool EnableTeleport = true;
        public bool EnableRepair = true;

        public bool EnableDesynthesis = true;
        public bool EnableBulkSupport = true;
        public bool EnableVentureCoffers = true;
        public bool EnableGachaCoffers = true;

        [NonSerialized]
        private DalamudPluginInterface? PluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.PluginInterface!.SavePluginConfig(this);
        }
    }
}
