using Dalamud.Interface.Windowing;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;

    public static ExcelSheet<Item> ItemSheet = null!;
    private static readonly Vector2 IconSize = new(28, 28);

    public MainWindow(Plugin plugin, Configuration configuration) : base("Tracky")
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 630),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        Configuration = configuration;

        ItemSheet = Plugin.Data.GetExcelSheet<Item>()!;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var buttonHeight = ImGui.CalcTextSize("RRRR").Y + (20.0f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginChild("SubContent", new Vector2(0, -buttonHeight)))
        {
            if (ImGui.BeginTabBar("##TrackerTabBar"))
            {
                DesynthesisTab();

                CofferTab();
            }
            ImGui.EndTabBar();
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

    private static void DrawIcon(uint iconId)
    {
        var texture = TexturesCache.Instance!.GetTextureFromIconId(iconId);
        ImGui.Image(texture.ImGuiHandle, IconSize);
    }
}
