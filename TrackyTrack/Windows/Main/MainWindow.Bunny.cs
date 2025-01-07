using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private CofferRarity Rarity = CofferRarity.Bronze;

    private Tabs SelectedBunnyTab;
    private static readonly Tabs[] BunnyTabs = [Tabs.Pagos, Tabs.Pyros, Tabs.Hydatos];

    private void BunnyTab()
    {
        using var tabItem = ImRaii.TabItem("Bunny");
        if (!tabItem.Success)
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

        var pos = ImGui.GetCursorPos();

        var nameDict = TabHelper.TabSize(BunnyTabs);
        var childSize = new Vector2(nameDict.Select(pair => pair.Value.Width).Max() + 10.0f, 0);
        using (var tabChild = ImRaii.Child("Tabs", childSize, true))
        {
            if (tabChild.Success)
            {
                if (ImGui.Selectable("Stats", SelectedBunnyTab == Tabs.Stats))
                    SelectedBunnyTab = Tabs.Stats;

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                foreach (var (id, (name, _)) in nameDict)
                {
                    var selected = SelectedBunnyTab == id;
                    if (ImGui.Selectable(name, selected))
                        SelectedBunnyTab = id;

                    using var pushedId = ImRaii.PushId((int) id);
                    using var pushedIndent = ImRaii.PushIndent(10.0f);
                    if (ImGui.Selectable("Bronze", selected && Rarity == CofferRarity.Bronze))
                    {
                        Rarity = CofferRarity.Bronze;
                        SelectedBunnyTab = id;
                    }
                    if (ImGui.Selectable("Silver", selected && Rarity == CofferRarity.Silver))
                    {
                        Rarity = CofferRarity.Silver;
                        SelectedBunnyTab = id;
                    }
                    if (ImGui.Selectable("Gold", selected && Rarity == CofferRarity.Gold))
                    {
                        Rarity = CofferRarity.Gold;
                        SelectedBunnyTab = id;
                    }
                }
            }
        }

        ImGui.SetCursorPos(pos with {X = pos.X + childSize.X});
        using (var contentChild = ImRaii.Child("Content", Vector2.Zero, true))
        {
            if (contentChild.Success)
            {
                switch (SelectedBunnyTab)
                {
                    case Tabs.Stats:
                        EurekaStats(characterCoffers);
                        break;
                    case Tabs.Pagos:
                        Pagos(characterCoffers);
                        break;
                    case Tabs.Pyros:
                        Pyros(characterCoffers);
                        break;
                    case Tabs.Hydatos:
                        Hydatos(characterCoffers);
                        break;
                }
            }
        }
    }

    private void EurekaStats(CharacterConfiguration[] characters)
    {
        var (worth, total, territoryCoffers) = EurekaUtil.GetAmounts(characters);

        using var table = ImRaii.Table("##TotalStatsTable", 2, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0));
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##stat", 0, 0.4f);
        ImGui.TableSetupColumn("##opened");

        ImGui.TableNextColumn();
        ImGui.TextColored(ImGuiColors.HealerGreen, "Opened");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{total:N0} Coffer{(total > 1 ? "s" : "")}");

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
        CofferHistory(Territory.Pagos, characters);
    }

    private void Pyros(CharacterConfiguration[] characters)
    {
        CofferHistory(Territory.Pyros, characters);
    }

    private void Hydatos(CharacterConfiguration[] characters)
    {
        CofferHistory(Territory.Hydatos, characters);
    }

    private void CofferHistory(Territory territory, CharacterConfiguration[] characters)
    {
        // fill dict with real values
        var dict = new Dictionary<uint, (uint Obtained, List<uint> Amounts)>();
        foreach (var pair in characters.SelectMany(c => c.Eureka.History).Where(pair => pair.Key == territory).Select(pair => pair.Value[Rarity]))
        {
            foreach (var result in pair.Values.SelectMany(result => result.Items))
            {
                if (!dict.TryAdd(result.Item, (1, [result.Count])))
                {
                    var current = dict[result.Item];
                    current.Obtained += 1;
                    current.Amounts.Add(result.Count);
                    dict[result.Item] = current;
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
            var item = Sheets.GetItem(pair.Key);
            var obtained = pair.Value.Obtained;
            var min = pair.Value.Amounts.Min();
            var max = pair.Value.Amounts.Max();
            var percentage = (double) pair.Value.Obtained / opened * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), obtained, min, max, percentage);
        });

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Gil Obtained: {opened * Rarity.ToWorth():N0}");
        new SimpleTable<Utils.SortedEntry>("##HistoryTable", Utils.SortEntries, ImGuiTableFlags.Sortable)
            .EnableSortSpec()
            .AddIconColumn("##icon", entry => Helper.DrawIcon(entry.Icon))
            .AddColumn("Item##item", entry => Helper.HoverableText(entry.Name))
            .AddColumn("Num##amount", entry => ImGui.TextUnformatted($"x{entry.Obtained}"), initWidth: 0.2f)
            .AddColumn("Pct##percentage", entry => ImGui.TextUnformatted($"{entry.Percentage:F2}%"), ImGuiTableColumnFlags.DefaultSort, 0.25f)
            .AddColumn("Min-Max##min-max", entry => ImGui.TextUnformatted($"{entry.Min}-{entry.Max}"), ImGuiTableColumnFlags.NoSort, initWidth: 0.2f)
            .Draw(unsortedList);
    }
}
