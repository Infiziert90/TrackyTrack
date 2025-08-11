using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private OccultCofferRarity PotRarity = OccultCofferRarity.Bronze;

    private Tabs SelectedPotTab;
    private static readonly Tabs[] PotTabs = [Tabs.SouthHorn];

    private void PotTab()
    {
        using var tabItem = ImRaii.TabItem("Pot");
        if (!tabItem.Success)
            return;

        if (Plugin.CharacterStorage.Values.Count == 0)
        {
            Helper.NoPotData();
            return;
        }

        var characterCoffers = Plugin.CharacterStorage.Values.Where(c => c.Occult.Opened > 0).ToArray();
        if (characterCoffers.Length == 0)
        {
            Helper.NoPotData();
            return;
        }

        var pos = ImGui.GetCursorPos();

        var nameDict = TabHelper.TabSize(PotTabs);
        var childSize = new Vector2(nameDict.Select(pair => pair.Value.Width).Max() + 10.0f, 0);
        using (var tabChild = ImRaii.Child("Tabs", childSize, true))
        {
            if (tabChild.Success)
            {
                if (ImGui.Selectable("Stats", SelectedPotTab == Tabs.Stats))
                    SelectedPotTab = Tabs.Stats;

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                foreach (var (id, (name, _)) in nameDict)
                {
                    var selected = SelectedPotTab == id;
                    if (ImGui.Selectable(name, selected))
                        SelectedPotTab = id;

                    using var pushedId = ImRaii.PushId((int) id);
                    using var pushedIndent = ImRaii.PushIndent(10.0f);
                    if (ImGui.Selectable(OccultCofferRarity.Bronze.ToName(), selected && PotRarity == OccultCofferRarity.Bronze))
                    {
                        PotRarity = OccultCofferRarity.Bronze;
                        SelectedPotTab = id;
                    }

                    if (ImGui.Selectable(OccultCofferRarity.Silver.ToName(), selected && PotRarity == OccultCofferRarity.Silver))
                    {
                        PotRarity = OccultCofferRarity.Silver;
                        SelectedPotTab = id;
                    }

                    if (ImGui.Selectable(OccultCofferRarity.Gold.ToName(), selected && PotRarity == OccultCofferRarity.Gold))
                    {
                        PotRarity = OccultCofferRarity.Gold;
                        SelectedPotTab = id;
                    }

                    if (ImGui.Selectable(OccultCofferRarity.BunnyGold.ToName(), selected && PotRarity == OccultCofferRarity.BunnyGold))
                    {
                        PotRarity = OccultCofferRarity.BunnyGold;
                        SelectedPotTab = id;
                    }
                }
            }
        }

        ImGui.SetCursorPos(pos with {X = pos.X + childSize.X});
        using (var contentChild = ImRaii.Child("Content", Vector2.Zero, true))
        {
            if (contentChild.Success)
            {
                switch (SelectedPotTab)
                {
                    case Tabs.Stats:
                        PotStats(characterCoffers);
                        break;
                    case Tabs.SouthHorn:
                        SouthHorn(characterCoffers);
                        break;
                }
            }
        }
    }

    private void PotStats(CharacterConfiguration[] characters)
    {
        var (worth, total, territoryCoffers) = OccultUtil.GetAmounts(characters);

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

    private void SouthHorn(CharacterConfiguration[] characters)
    {
        CofferHistory(OccultTerritory.SouthHorn, characters);
    }

    private void CofferHistory(OccultTerritory territory, CharacterConfiguration[] characters)
    {
        // fill dict with real values
        var dict = new Dictionary<uint, (uint Obtained, List<uint> Amounts)>();
        foreach (var pair in characters.SelectMany(c => c.Occult.History).Where(pair => pair.Key == territory).Select(pair => pair.Value[PotRarity]))
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
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Haven't opened any {PotRarity.ToName()} Coffer in {territory.ToName()}");
            return;
        }

        var opened = characters.Select(c => c.Occult.History[territory][PotRarity].Count).Sum();
        var unsortedList = Utils.ToSortedEntry(dict, opened);

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Gil Obtained: {opened * PotRarity.ToWorth():N0}");
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
