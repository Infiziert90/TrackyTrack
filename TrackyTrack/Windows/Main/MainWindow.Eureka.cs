using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private CofferRarity Rarity = CofferRarity.Bronze;

    private void BunnyTab()
    {
        if (ImGui.BeginTabItem("Bunny"))
        {
            if (ImGui.BeginTabBar("##BunnyTabBar"))
            {
                Pagos();

                Pyros();

                Hydatos();

                ImGui.EndTabBar();
            }
            ImGui.EndTabItem();
        }
    }

    private void Pagos()
    {
        if (!ImGui.BeginTabItem("Pagos"))
            return;

        CofferHistory(Territory.Pagos);

        ImGui.EndTabItem();
    }

    private void Pyros()
    {
        if (!ImGui.BeginTabItem("Pyros"))
            return;

        CofferHistory(Territory.Pyros);

        ImGui.EndTabItem();
    }

    private void Hydatos()
    {
        if (!ImGui.BeginTabItem("Hydatos"))
            return;

        CofferHistory(Territory.Hydatos);

        ImGui.EndTabItem();
    }

    private void CofferHistory(Territory territory)
    {
        var characters = Plugin.CharacterStorage.Values.ToArray();
        if (!characters.Any())
        {
            Helper.NoEurekaCofferData();
            return;
        }

        var characterCoffers = characters.Where(c => c.Eureka.Opened > 0).ToList();
        if (!characterCoffers.Any())
        {
            Helper.NoEurekaCofferData();
            return;
        }

        var rarity = (int) Rarity;
        ImGui.RadioButton("Bronze", ref rarity, (int) CofferRarity.Bronze);
        ImGui.SameLine();
        ImGui.RadioButton("Silver", ref rarity, (int) CofferRarity.Silver);
        ImGui.SameLine();
        ImGui.RadioButton("Gold", ref rarity, (int) CofferRarity.Gold);
        Rarity = (CofferRarity) rarity;

        // fill dict with real values
        var dict = new Dictionary<uint, (uint Total, uint Obtained)>();
        foreach (var pair in characterCoffers.SelectMany(c => c.Eureka.History).Where(pair => pair.Key == territory).Select(pair => pair.Value[Rarity]))
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

        if (!dict.Any())
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Haven't opened any {Rarity.ToName()} Coffer in {territory.ToName()}");
            return;
        }

        var opened = characterCoffers.Select(c => c.Eureka.History[territory][Rarity].Count).Sum();
        var unsortedList = dict.Where(pair => pair.Value.Obtained > 0).Select(pair =>
        {
            var item = ItemSheet.GetRow(pair.Key)!;
            var count = pair.Value.Total;
            var percentage = (double) pair.Value.Obtained / opened * 100.0;
            return new Utils.SortedEntry(item.Icon, Utils.ToStr(item.Name), count, percentage);
        });

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Gil Obtained: {opened * Rarity.ToWorth():N0}");
        if (ImGui.BeginTable($"##HistoryTable", 4, ImGuiTableFlags.Sortable))
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.NoSort, 0.17f);
            ImGui.TableSetupColumn("Item##item");
            ImGui.TableSetupColumn("Num##amount", 0, 0.2f);
            ImGui.TableSetupColumn("Pct##percentage", ImGuiTableColumnFlags.DefaultSort, 0.25f);

            ImGui.TableHeadersRow();

            ImGui.Indent(10.0f);
            foreach (var sortedEntry in Utils.SortEntries(unsortedList, ImGui.TableGetSortSpecs().Specs))
            {
                ImGui.TableNextColumn();
                DrawIcon(sortedEntry.Icon);
                ImGui.TableNextColumn();

                ImGui.TextUnformatted(sortedEntry.Name);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(sortedEntry.Name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{sortedEntry.Count}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{sortedEntry.Percentage:F2}%");
                ImGui.TableNextRow();
            }

            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }
    }
}
