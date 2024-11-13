using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;

    public MainWindow(Plugin plugin, Configuration configuration) : base("Tracky##TrackyTrack")
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 630),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        Configuration = configuration;

        InitializeStats();
        InitializeDesynth();
    }

    public void Dispose() { }

    public override void Draw()
    {
        var buttonHeight = ImGui.CalcTextSize("RRRR").Y + (20.0f * ImGuiHelpers.GlobalScale);
        using (var contentChild = ImRaii.Child("SubContent", new Vector2(0, -buttonHeight)))
        {
            if (contentChild.Success)
            {
                using var tabBar = ImRaii.TabBar("##TrackerTabBar");
                if (tabBar.Success)
                {
                    StatsTab();

                    DesynthesisTab();

                    CofferTab();

                    GachaTab();

                    BunnyTab();

                    LockboxTab();
                }
            }
        }

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(1.0f);

        using var bottomChild = ImRaii.Child("BottomBar", Vector2.Zero, false, 0);
        if (!bottomChild.Success)
            return;

        Helper.MainMenuIcon(Plugin);
    }
}
