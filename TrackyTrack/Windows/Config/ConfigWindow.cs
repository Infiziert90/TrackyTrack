using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace TrackyTrack.Windows.Config;

public partial class ConfigWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;

    public ConfigWindow(Plugin plugin) : base("Configuration##TrackyTrack")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 460),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("##ConfigTabBar");
        if (!tabBar.Success)
            return;

        Modules();

        Upload();

        About();
    }
}
