using System.ComponentModel;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private long LastSessionRefresh;
    private const int SessionRefreshRate = 5_000; // 5s

    private readonly SortedList<TrackedSessionStats, int> TrackedStats = new();

    public void InitSession()
    {
        foreach (var stat in Enum.GetValues<TrackedSessionStats>())
            TrackedStats[stat] = 0;
    }

    private void SessionTab()
    {
        using var tabItem = ImRaii.TabItem("Session##SessionTab");
        if (!tabItem.Success)
            return;

        Session();
    }

    private void Session()
    {
        var characters = Plugin.CharacterStorage.Values.ToArray();
        if (characters.Length == 0)
        {
            Helper.NoCharacters();
            return;
        }

        RefreshSession(characters);

        using var child = ImRaii.Child("ContentChild", Vector2.Zero, true);
        if (!child.Success)
            return;

        SessionStats();
    }

    private void SessionStats()
    {
        ImGui.TextColored(ImGuiColors.DalamudOrange, "= Work in Progress =");
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Session Changes:");

        using var indent = ImRaii.PushIndent(10.0f);
        using var table = ImRaii.Table("##StatsTable", 2, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0));
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##Stat", ImGuiTableColumnFlags.WidthStretch, 0.6f);
        ImGui.TableSetupColumn("##Num");

        foreach (var stat in Enum.GetValues<TrackedSessionStats>())
        {
            if (TrackedStats[stat] == 0)
                continue;

            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, stat.GetDescription());

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{TrackedStats[stat]:N0}");

            ImGui.TableNextRow();
        }
    }

    private void RefreshSession(CharacterConfiguration[] characters)
    {
        if (Utils.NeedsRefresh(ref LastSessionRefresh, SessionRefreshRate))
        {
            if (Plugin.SessionCopyState != SessionState.Done)
                return;

            var (_, _, territoryCoffers) = EurekaUtil.GetAmounts(characters);
            var (_, _, territoryCoffersCopy) = EurekaUtil.GetAmounts(Plugin.SessionCharacterCopy.Values);

            var pagos = (Old: territoryCoffersCopy[Territory.Pagos], New: territoryCoffers[Territory.Pagos]);
            TrackedStats[TrackedSessionStats.PagosBronze] = pagos.New[CofferRarity.Bronze] - pagos.Old[CofferRarity.Bronze];
            TrackedStats[TrackedSessionStats.PagosSilver] = pagos.New[CofferRarity.Silver] - pagos.Old[CofferRarity.Silver];
            TrackedStats[TrackedSessionStats.PagosGold] = pagos.New[CofferRarity.Gold] - pagos.Old[CofferRarity.Gold];

            var pyros = (Old: territoryCoffersCopy[Territory.Pyros], New: territoryCoffers[Territory.Pyros]);
            TrackedStats[TrackedSessionStats.PyrosBronze] = pyros.New[CofferRarity.Bronze] - pyros.Old[CofferRarity.Bronze];
            TrackedStats[TrackedSessionStats.PyrosSilver] = pyros.New[CofferRarity.Silver] - pyros.Old[CofferRarity.Silver];
            TrackedStats[TrackedSessionStats.PyrosGold] = pyros.New[CofferRarity.Gold] - pyros.Old[CofferRarity.Gold];

            var hydatos = (Old: territoryCoffersCopy[Territory.Hydatos], New: territoryCoffers[Territory.Hydatos]);
            TrackedStats[TrackedSessionStats.HydatosBronze] = hydatos.New[CofferRarity.Bronze] - hydatos.Old[CofferRarity.Bronze];
            TrackedStats[TrackedSessionStats.HydatosSilver] = hydatos.New[CofferRarity.Silver] - hydatos.Old[CofferRarity.Silver];
            TrackedStats[TrackedSessionStats.HydatosGold] = hydatos.New[CofferRarity.Gold] - hydatos.Old[CofferRarity.Gold];
        }
    }

    public enum TrackedSessionStats
    {
        [Description("Pagos Bronze")] PagosBronze,
        [Description("Pagos Silver")] PagosSilver,
        [Description("Pagos Gold")] PagosGold,

        [Description("Pyros Bronze")] PyrosBronze,
        [Description("Pyros Silver")] PyrosSilver,
        [Description("Pyros Gold")] PyrosGold,

        [Description("Hydatos Bronze")] HydatosBronze,
        [Description("Hydatos Silver")] HydatosSilver,
        [Description("Hydatos Gold")] HydatosGold,
    }
}
