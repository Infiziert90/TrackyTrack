using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private CofferRarity Rarity = CofferRarity.Bronze;

    private void BunnyTab()
    {
        using var tabItem = ImRaii.TabItem("Bunny");
        if (!tabItem.Success)
            return;

        using var tabBar = ImRaii.TabBar("##BunnyTabBar");
        if (!tabBar.Success)
            return;

        if (Plugin.CharacterStorage.Values.Count == 0)
        {
            Helper.NoEurekaCofferData();
            return;
        }

        var characterCoffers = Plugin.CharacterStorage.Values.Where(c => c.Eureka.Opened > 0).ToArray();
        if (characterCoffers.Length == 0)
        {
            Helper.NoEurekaCofferData();
            return;
        }

        EurekaStats(characterCoffers);

        Pagos(characterCoffers);

        Pyros(characterCoffers);

        Hydatos(characterCoffers);
    }

    private void EurekaStats(CharacterConfiguration[] characters)
    {
        using var tabItem = ImRaii.TabItem("Stats");
        if (!tabItem.Success)
            return;

        var worth = 0L;
        var totalNumber = 0;
        var territoryCoffers = new Dictionary<Territory, Dictionary<CofferRarity, int>>();
        foreach (var (territory, rarityDictionary) in characters.SelectMany(c => c.Eureka.History))
        {
            territoryCoffers.TryAdd(territory, new Dictionary<CofferRarity, int>());
            foreach (var (rarity, history) in rarityDictionary)
            {
                totalNumber += history.Count;
                worth += history.Count * rarity.ToWorth();

                if (!territoryCoffers[territory].TryAdd(rarity, history.Count))
                    territoryCoffers[territory][rarity] += history.Count;
            }
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextColored(ImGuiColors.DalamudViolet, "General:");
        using var table = ImRaii.Table("##TotalStatsTable", 2, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0));
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##stat", 0, 0.4f);
        ImGui.TableSetupColumn("##opened");

        using var indent = ImRaii.PushIndent(10.0f);
        ImGui.TableNextColumn();
        ImGui.TextColored(ImGuiColors.HealerGreen, "Opened");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{totalNumber:N0} Coffer{(totalNumber > 1 ? "s" : "")}");

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextColored(ImGuiColors.HealerGreen, "Worth");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{worth:N0} Gil");

        foreach (var (territory, rarityDictionary) in territoryCoffers)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, territory.ToName());

            using var innerIndent = ImRaii.PushIndent(10.0f);
            foreach (var (rarity, rarityCount) in rarityDictionary)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, rarity.ToName());

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{rarityCount:N0}  Coffer{(rarityCount > 1 ? "s" : "")}");
            }
        }
    }

    private void Pagos(CharacterConfiguration[] characters)
    {
        using var tabItem = ImRaii.TabItem("Pagos");
        if (!tabItem.Success)
            return;

        CofferHistory(Territory.Pagos, characters);
    }

    private void Pyros(CharacterConfiguration[] characters)
    {
        using var tabItem = ImRaii.TabItem("Pyros");
        if (!tabItem.Success)
            return;

        CofferHistory(Territory.Pyros, characters);
    }

    private void Hydatos(CharacterConfiguration[] characters)
    {
        using var tabItem = ImRaii.TabItem("Hydatos");
        if (!tabItem.Success)
            return;

        CofferHistory(Territory.Hydatos, characters);
    }

    private void CofferHistory(Territory territory, CharacterConfiguration[] characters)
    {
        var rarity = (int) Rarity;
        ImGui.RadioButton("Bronze", ref rarity, (int) CofferRarity.Bronze);
        ImGui.SameLine();
        ImGui.RadioButton("Silver", ref rarity, (int) CofferRarity.Silver);
        ImGui.SameLine();
        ImGui.RadioButton("Gold", ref rarity, (int) CofferRarity.Gold);
        Rarity = (CofferRarity) rarity;

        // fill dict with real values
        var dict = new Dictionary<uint, (uint Total, uint Obtained)>();
        foreach (var pair in characters.SelectMany(c => c.Eureka.History).Where(pair => pair.Key == territory).Select(pair => pair.Value[Rarity]))
        {
            foreach (var result in pair.Values.SelectMany(result => result.Items))
            {
                if (!dict.TryAdd(result.Item, (result.Count, 1)))
                {
                    var current = dict[result.Item];
                    dict[result.Item] = (current.Total + result.Count, current.Obtained + 1);
                }
            }
        }

        if (dict.Count == 0)
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Haven't opened any {Rarity.ToName()} Coffer in {territory.ToName()}");
            return;
        }

        var opened = characters.Select(c => c.Eureka.History[territory][Rarity].Count).Sum();
        var unsortedList = dict.Where(pair => pair.Value.Obtained > 0).Select(pair =>
        {
            var item = ItemSheet.GetRow(pair.Key)!;
            var count = pair.Value.Total;
            var percentage = (double) pair.Value.Obtained / opened * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), count, percentage);
        });

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Gil Obtained: {opened * Rarity.ToWorth():N0}");
        new SortableTable("##HistoryTable", unsortedList, ImGuiTableFlags.Sortable, 10.0f)
            .AddColumn("##icon", ImGuiTableColumnFlags.NoSort, 0.17f)
            .AddAction(entry => Helper.DrawIcon(entry.Icon))
            .AddColumn("Item##item")
            .AddAction(entry => Helper.HoverableText(entry.Name))
            .AddColumn("Num##amount", initWidth: 0.2f)
            .AddAction(entry => ImGui.TextUnformatted($"x{entry.Count}"))
            .AddColumn("Pct##percentage", ImGuiTableColumnFlags.DefaultSort, 0.25f)
            .AddAction(entry => ImGui.TextUnformatted($"{entry.Percentage:F2}%"))
            .Draw();
    }
}
