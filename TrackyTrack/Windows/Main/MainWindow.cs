using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;

    private static ExcelSheet<Item> ItemSheet = null!;
    private static ExcelSheet<GCSupplyDutyReward> GCSupplySheet = null!;

    public MainWindow(Plugin plugin, Configuration configuration) : base("Tracky##TrackyTrack")
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 630),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        Configuration = configuration;

        ItemSheet = Plugin.Data.GetExcelSheet<Item>()!;
        GCSupplySheet = Plugin.Data.GetExcelSheet<GCSupplyDutyReward>()!;

        InitializeStats();
        InitializeDesynth();
    }

    public void Dispose() { }

    public override void Draw()
    {
        var buttonHeight = ImGui.CalcTextSize("RRRR").Y + (20.0f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginChild("SubContent", new Vector2(0, -buttonHeight)))
        {
            if (ImGui.BeginTabBar("##TrackerTabBar"))
            {
                StatsTab();

                DesynthesisTab();

                CofferTab();

                GachaTab();

                BunnyTab();

                LockboxTab();

                ImGui.EndTabBar();
            }
        }
        ImGui.EndChild();

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(1.0f);

        if (ImGui.BeginChild("BottomBar", new Vector2(0, 0), false, 0))
        {
            Helper.MainMenuIcon(Plugin);
        }
        ImGui.EndChild();
    }
}
