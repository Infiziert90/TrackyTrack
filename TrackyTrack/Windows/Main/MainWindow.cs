using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow : Window, IDisposable
{
    private readonly Plugin Plugin;
    private readonly Configuration Configuration;

    public MainWindow(Plugin plugin, Configuration configuration) : base("Tracky##TrackyTrack")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(480, 650),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        Configuration = configuration;

        InitializeStats();
        InitializeDesynth();
        InitSession();
    }

    public void Dispose() { }

    public override void Draw()
    {
        var buttonHeight = Helper.CalculateChildHeight();
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

                    OccultTab();

                    LockboxTab();

                    SaucerTab();

                    SessionTab();
                }
            }
        }

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(Helper.GetSeparatorPaddingHeight);

        using var bottomChild = ImRaii.Child("BottomBar", Vector2.Zero);
        if (!bottomChild.Success)
            return;

        Helper.MainMenuIcon(Plugin);
    }
}
