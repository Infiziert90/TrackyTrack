using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private OccultTreasureRarity TreasureRarity = OccultTreasureRarity.Bronze;
    private OccultCofferRarity PotRarity = OccultCofferRarity.Bronze;
    private OccultCofferRarity CarrotRarity = OccultCofferRarity.BunnyGold;

    private Tabs SelectedOccultTab;
    private static readonly Tabs[] PotTabs = [Tabs.Treasure, Tabs.Pot, Tabs.Carrot];

    private void OccultTab()
    {
        using var tabItem = ImRaii.TabItem("Occult");
        if (!tabItem.Success)
            return;

        if (Plugin.CharacterStorage.Values.Count == 0)
        {
            Helper.NoPotData();
            return;
        }

        var characterCoffers = Plugin.CharacterStorage.Values.Where(c => c.Occult.Opened > 0 || c.Occult.TreasureOpened > 0).ToArray();
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
                if (ImGui.Selectable("Stats", SelectedOccultTab == Tabs.Stats))
                    SelectedOccultTab = Tabs.Stats;

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                foreach (var (id, (name, _)) in nameDict)
                {
                    var selected = SelectedOccultTab == id;
                    if (ImGui.Selectable(name, selected))
                        SelectedOccultTab = id;

                    using var pushedId = ImRaii.PushId((int) id);
                    using var pushedIndent = ImRaii.PushIndent(10.0f);
                    if (id == Tabs.Treasure)
                    {
                        if (ImGui.Selectable(OccultTreasureRarity.Bronze.ToName(), selected && TreasureRarity == OccultTreasureRarity.Bronze))
                        {
                            TreasureRarity = OccultTreasureRarity.Bronze;
                            SelectedOccultTab = id;
                        }

                        if (ImGui.Selectable(OccultTreasureRarity.Silver.ToName(), selected && TreasureRarity == OccultTreasureRarity.Silver))
                        {
                            TreasureRarity = OccultTreasureRarity.Silver;
                            SelectedOccultTab = id;
                        }
                    }
                    else if (id == Tabs.Pot)
                    {
                        if (ImGui.Selectable(OccultCofferRarity.Bronze.ToName(), selected && PotRarity == OccultCofferRarity.Bronze))
                        {
                            PotRarity = OccultCofferRarity.Bronze;
                            SelectedOccultTab = id;
                        }

                        if (ImGui.Selectable(OccultCofferRarity.Silver.ToName(), selected && PotRarity == OccultCofferRarity.Silver))
                        {
                            PotRarity = OccultCofferRarity.Silver;
                            SelectedOccultTab = id;
                        }

                        if (ImGui.Selectable(OccultCofferRarity.Gold.ToName(), selected && PotRarity == OccultCofferRarity.Gold))
                        {
                            PotRarity = OccultCofferRarity.Gold;
                            SelectedOccultTab = id;
                        }
                    }
                    else
                    {
                        if (ImGui.Selectable(OccultCofferRarity.BunnyGold.ToName(), selected && CarrotRarity == OccultCofferRarity.BunnyGold))
                        {
                            CarrotRarity = OccultCofferRarity.BunnyGold;
                            SelectedOccultTab = id;
                        }
                    }
                }
            }
        }

        ImGui.SetCursorPos(pos with {X = pos.X + childSize.X});
        using (var contentChild = ImRaii.Child("Content", Vector2.Zero, true))
        {
            if (contentChild.Success)
            {
                switch (SelectedOccultTab)
                {
                    case Tabs.Stats:
                        PotStats(characterCoffers);
                        break;
                    case Tabs.Treasure:
                        Treasure(characterCoffers);
                        break;
                    case Tabs.Pot:
                        Pot(characterCoffers);
                        break;
                    case Tabs.Carrot:
                        Carrot(characterCoffers);
                        break;
                }
            }
        }
    }

    private void PotStats(CharacterConfiguration[] characters)
    {
        var (treasureTotal, treasureCoffers) = OccultUtil.GetTreasureAmounts(characters);
        var (potWorth, potTotal, potCoffers) = OccultUtil.GetPotAmounts(characters);

        using var table = ImRaii.Table("##TotalStatsTable", 2, 0, new Vector2(400 * ImGuiHelpers.GlobalScale, 0));
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##stat", ImGuiTableColumnFlags.WidthStretch, 0.4f);
        ImGui.TableSetupColumn("##opened");

        ImGui.TableNextColumn();
        ImGui.TextColored(ImGuiColors.DalamudOrange, "Treasure");

        using (ImRaii.PushIndent(10.0f))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Opened");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{treasureTotal:N0} Coffer{(treasureTotal > 1 ? "s" : "")}");

            ImGui.TableNextRow();

            foreach (var (territory, rarityDictionary) in treasureCoffers)
            {
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

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextColored(ImGuiColors.DalamudOrange, "Pots");

        using (ImRaii.PushIndent(10.0f))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Opened");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{potTotal:N0} Coffer{(potTotal > 1 ? "s" : "")}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Worth");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{potWorth:N0} Gil");

            foreach (var (territory, rarityDictionary) in potCoffers)
            {
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
    }

    private void Treasure(CharacterConfiguration[] characters)
    {
        TreasureHistory(OccultTerritory.SouthHorn, characters);
    }

    private void Pot(CharacterConfiguration[] characters)
    {
        CofferHistory(OccultTerritory.SouthHorn, PotRarity, characters);
    }

    private void Carrot(CharacterConfiguration[] characters)
    {
        CofferHistory(OccultTerritory.SouthHorn, CarrotRarity, characters);
    }

    private void TreasureHistory(OccultTerritory territory, CharacterConfiguration[] characters)
    {
        // fill dict with real values
        var dict = new Dictionary<uint, (uint Obtained, List<uint> Amounts)>();
        foreach (var pair in characters.SelectMany(c => c.Occult.TreasureHistory).Where(pair => pair.Key == territory).Select(pair => pair.Value[TreasureRarity]))
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
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Haven't opened any {TreasureRarity.ToName()} Treasure in {territory.ToName()}");
            return;
        }

        var opened = characters.Select(c => c.Occult.TreasureHistory[territory][TreasureRarity].Count).Sum();
        var unsortedList = Utils.ToSortedEntry(dict, opened);

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        new SimpleTable<Utils.SortedEntry>("##HistoryTable", Utils.SortEntries, ImGuiTableFlags.Sortable)
            .EnableSortSpec()
            .AddIconColumn("##icon", entry => Helper.DrawIcon(entry.Icon))
            .AddColumn("Item##item", entry => Helper.HoverableText(entry.Name))
            .AddColumn("Num##amount", entry => ImGui.TextUnformatted($"x{entry.Obtained}"), initWidth: 0.2f)
            .AddColumn("Pct##percentage", entry => ImGui.TextUnformatted($"{entry.Percentage:F2}%"), ImGuiTableColumnFlags.DefaultSort, 0.25f)
            .AddColumn("Min-Max##min-max", entry => ImGui.TextUnformatted($"{entry.Min}-{entry.Max}"), ImGuiTableColumnFlags.NoSort, initWidth: 0.2f)
            .Draw(unsortedList);
    }

    private void CofferHistory(OccultTerritory territory, OccultCofferRarity rarity, CharacterConfiguration[] characters)
    {
        // fill dict with real values
        var dict = new Dictionary<uint, (uint Obtained, List<uint> Amounts)>();
        foreach (var pair in characters.SelectMany(c => c.Occult.History).Where(pair => pair.Key == territory).Select(pair => pair.Value[rarity]))
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
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Haven't opened any {rarity.ToName()} Coffer in {territory.ToName()}");
            return;
        }

        var opened = characters.Select(c => c.Occult.History[territory][rarity].Count).Sum();
        var unsortedList = Utils.ToSortedEntry(dict, opened);

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Gil Obtained: {opened * rarity.ToWorth():N0}");
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
