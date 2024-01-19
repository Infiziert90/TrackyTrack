using Dalamud.Interface.Utility;
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
                var characters = Plugin.CharacterStorage.Values;
                if (!characters.Any())
                {
                    Helper.NoEurekaCofferData();

                    ImGui.EndTabBar();
                    ImGui.EndTabItem();
                    return;
                }

                var characterCoffers = characters.Where(c => c.Eureka.Opened > 0).ToArray();
                if (!characterCoffers.Any())
                {
                    Helper.NoEurekaCofferData();

                    ImGui.EndTabBar();
                    ImGui.EndTabItem();
                    return;
                }

                EurekaStats(characterCoffers);

                Pagos(characterCoffers);

                Pyros(characterCoffers);

                Hydatos(characterCoffers);

                ImGui.EndTabBar();
            }
            ImGui.EndTabItem();
        }
    }

    private void EurekaStats(CharacterConfiguration[] characters)
    {
        if (!ImGui.BeginTabItem("Stats"))
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
        if (ImGui.BeginTable($"##TotalStatsTable", 2, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0)))
        {
            ImGui.TableSetupColumn("##stat", 0, 0.4f);
            ImGui.TableSetupColumn("##opened");

            ImGui.TableNextColumn();
            ImGui.Indent(10.0f);
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

                ImGui.Indent(10.0f);
                foreach (var (rarity, rarityCount) in rarityDictionary)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.HealerGreen, rarity.ToName());

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{rarityCount:N0}  Coffer{(rarityCount > 1 ? "s" : "")}");
                }
                ImGui.Unindent(10.0f);
            }
            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }

    private void Pagos(CharacterConfiguration[] characters)
    {
        if (!ImGui.BeginTabItem("Pagos"))
            return;

        CofferHistory(Territory.Pagos, characters);

        ImGui.EndTabItem();
    }

    private void Pyros(CharacterConfiguration[] characters)
    {
        if (!ImGui.BeginTabItem("Pyros"))
            return;

        CofferHistory(Territory.Pyros, characters);

        ImGui.EndTabItem();
    }

    private void Hydatos(CharacterConfiguration[] characters)
    {
        if (!ImGui.BeginTabItem("Hydatos"))
            return;

        CofferHistory(Territory.Hydatos, characters);

        ImGui.EndTabItem();
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

        if (!dict.Any())
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
                Helper.DrawIcon(sortedEntry.Icon);
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
