using Dalamud.Logging;
using TrackyTrack.Data;
using Lumina.Excel.GeneratedSheets;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private int SelectedCharacter;
    private int SelectedHistory;

    private uint SourceSearchResult;
    private uint ItemSearchResult;

    private static readonly ExcelSheetSelector.ExcelSheetPopupOptions<Item> SourceOptions = new()
    {
        FormatRow = a => a.RowId switch { _ => $"[#{a.RowId}] {Utils.ToStr(a.Name)}" },
        FilteredSheet = Plugin.Data.GetExcelSheet<Item>()!.Skip(1).Where(i => Utils.ToStr(i.Name) != "").Where(i => i.Desynth > 0)
    };

    private static readonly ExcelSheetSelector.ExcelSheetPopupOptions<Item> ItemOptions = new()
    {
        FormatRow = a => a.RowId switch { _ => $"[#{a.RowId}] {Utils.ToStr(a.Name)}" },
        FilteredSheet = Plugin.Data.GetExcelSheet<Item>()!.Skip(1).Where(i => Utils.ToStr(i.Name) != "")
    };

    private void DesynthesisTab()
    {
        if (ImGui.BeginTabItem("Desynthesis"))
        {
            var characters = Plugin.CharacterStorage.Values.ToArray();
            if (!characters.Any())
            {
                Helper.NoDesynthesisData();

                ImGui.EndTabItem();
                return;
            }

            if (ImGui.BeginTabBar("##DesynthTabBar"))
            {
                Stats(characters);

                History();

                Rewards(characters);

                Source(characters);

                SourceSearch(characters);

                ItemSearch(characters);

                ImGui.EndTabBar();
            }
            ImGui.EndTabItem();
        }
    }

    private void Stats(CharacterConfiguration[] characters)
    {
        if (!ImGui.BeginTabItem("Stats"))
            return;

        var totalNumber = characters.Sum(c => c.Storage.History.Count);
        var dict = new Dictionary<uint, uint>();
        foreach (var pair in characters.SelectMany(c => c.Storage.Total))
        {
            if (!dict.TryAdd(pair.Key, pair.Value))
                dict[pair.Key] += pair.Value;
        }

        var numberOfDesynthesis = new Dictionary<uint, uint>();
        foreach (var pair in characters.SelectMany(c => c.Storage.History))
        {
            if (!numberOfDesynthesis.TryAdd(pair.Value.Source, 1))
                numberOfDesynthesis[pair.Value.Source] += 1;
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.BeginTable($"##TotalStatsTable", 3))
        {
            ImGui.TableSetupColumn("##stat", 0, 0.55f);
            ImGui.TableSetupColumn("##name");
            ImGui.TableSetupColumn("##amount");

            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Desynthesis");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{totalNumber:N0} time{(totalNumber > 1 ? "s" : "")}");
            ImGui.TableNextRow();

            var destroyed = numberOfDesynthesis.MaxBy(pair => pair.Value);
            var item = ItemSheet.GetRow(destroyed.Key)!;
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Most Often");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{Utils.ToStr(item.Name)}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{destroyed.Value}");

            var bestItem = dict.Where(pair => pair.Key is > 20 and < 1000000).MaxBy(pair => pair.Value);
            item = ItemSheet.GetRow(bestItem.Key)!;
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Best Reward");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{Utils.ToStr(item.Name)}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{bestItem.Value}");

            var bestCrystal = dict.Where(pair => pair.Key is > 0 and < 20).MaxBy(pair => pair.Value);
            item = ItemSheet.GetRow(bestCrystal.Key)!;
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Best Crystal");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{Utils.ToStr(item.Name)}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{bestCrystal.Value}");

            var sum = 0UL;
            foreach (var pair in dict.Where(pair => Items.Worth.ContainsKey(pair.Key)))
                sum += Items.Worth[pair.Key] * pair.Value;

            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Pure Gil Made");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{sum:N0}");
            ImGui.TableNextRow();

            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }

    private void History()
    {
        if (!ImGui.BeginTabItem("History"))
            return;

        var existingCharacters = Plugin.CharacterStorage.Values
                                       .Select(character => $"{character.CharacterName}@{character.World}")
                                       .ToArray();

        var selectedCharacter = SelectedCharacter;
        ImGui.Combo("##existingCharacters", ref selectedCharacter, existingCharacters, existingCharacters.Length);
        if (selectedCharacter != SelectedCharacter)
        {
            SelectedCharacter = selectedCharacter;
            SelectedHistory = 0;
        }

        var selectedChar = Plugin.CharacterStorage.Values.ToList()[SelectedCharacter];
        var history = selectedChar.Storage.History.Reverse().Select(pair => $"{pair.Key}").ToArray();
        if (!history.Any())
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, "Tracking starts after your first desynthesis.");
            ImGui.EndTabItem();
            return;
        }

        ImGui.Combo("##voyageSelection", ref SelectedHistory, history, history.Length);
        Helper.DrawArrows(ref SelectedHistory, history.Length);

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        var resultPair = selectedChar.Storage.History.Reverse().ToArray()[SelectedHistory];

        var source = ItemSheet.GetRow(resultPair.Value.Source)!;
        DrawIcon(source.Icon);
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.HealerGreen, Utils.ToStr(source.Name));

        if (ImGui.BeginTable($"##HistoryTable", 3))
        {
            ImGui.TableSetupColumn("##icon", 0, 0.2f);
            ImGui.TableSetupColumn("##item");
            ImGui.TableSetupColumn("##amount", 0, 0.2f);

            ImGui.Indent(10.0f);
            foreach (var result in resultPair.Value.Received.Where(i => i.Item > 0))
            {
                var item = ItemSheet.GetRow(result.Item)!;

                ImGui.TableNextColumn();
                DrawIcon(item.Icon);
                ImGui.TableNextColumn();

                var name = Utils.ToStr(item.Name);
                ImGui.TextUnformatted(name);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Utils.ToStr(item.Name));

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{result.Count}");
                ImGui.TableNextRow();
            }

            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }

    private static void Rewards(IEnumerable<CharacterConfiguration> characters)
    {
        if (!ImGui.BeginTabItem("Rewards"))
            return;

        var dict = new Dictionary<uint, uint>();
        foreach (var pair in characters.SelectMany(c => c.Storage.Total))
        {
            if (!dict.TryAdd(pair.Key, pair.Value))
                dict[pair.Key] += pair.Value;
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.BeginChild("RewardsTableChild"))
        {
            if (ImGui.BeginTable($"##RewardsTable", 3))
            {
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, IconSize.X + 10.0f);
                ImGui.TableSetupColumn("##item");
                ImGui.TableSetupColumn("##amount", 0, 0.2f);

                ImGui.Indent(10.0f);
                foreach (var (itemId, count) in dict.Where(pair => pair.Key is > 0 and < 1000000).OrderBy(pair => pair.Key))
                {
                    var item = ItemSheet.GetRow(itemId)!;

                    ImGui.TableNextColumn();
                    DrawIcon(item.Icon);
                    ImGui.TableNextColumn();

                    var name = Utils.ToStr(item.Name);
                    if (ImGui.Selectable(name))
                        ImGui.SetClipboardText(name);

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Utils.ToStr(item.Name));

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"x{count}");
                    ImGui.TableNextRow();
                }

                ImGui.Unindent(10.0f);
                ImGui.EndTable();
            }
        }
        ImGui.EndChild();
        ImGui.EndTabItem();
    }

    private static void Source(IEnumerable<CharacterConfiguration> characters)
    {
        if (!ImGui.BeginTabItem("Sources"))
            return;

        var numberOfDesynthesis = new Dictionary<uint, uint>();
        foreach (var pair in characters.SelectMany(c => c.Storage.History))
        {
            if (!numberOfDesynthesis.TryAdd(pair.Value.Source, 1))
                numberOfDesynthesis[pair.Value.Source] += 1;
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.BeginChild("SourceTableChild"))
        {
            if (ImGui.BeginTable($"##DesynthesisSourceTable", 3))
            {
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, IconSize.X + 10.0f);
                ImGui.TableSetupColumn("##item");
                ImGui.TableSetupColumn("##amount", 0, 0.2f);

                ImGui.Indent(10.0f);
                foreach (var (source, count) in numberOfDesynthesis.OrderByDescending(pair => pair.Value))
                {
                    var item = ItemSheet.GetRow(source)!;

                    ImGui.TableNextColumn();
                    DrawIcon(item.Icon);
                    ImGui.TableNextColumn();

                    var name = Utils.ToStr(item.Name);
                    if (ImGui.Selectable(name))
                        ImGui.SetClipboardText(name);

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Utils.ToStr(item.Name));

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"x{count:N0}");
                    ImGui.TableNextRow();
                }

                ImGui.Unindent(10.0f);
                ImGui.EndTable();
            }
        }
        ImGui.EndChild();

        ImGui.EndTabItem();
    }

    private void SourceSearch(IEnumerable<CharacterConfiguration> characters)
    {
        if (!ImGui.BeginTabItem("Search Source"))
            return;

        var historyDict = new Dictionary<uint, List<DesynthResult>>();
        foreach (var pair in characters.SelectMany(c => c.Storage.History))
        {
            if (!historyDict.TryAdd(pair.Value.Source, new List<DesynthResult> {pair.Value}))
                historyDict[pair.Value.Source].Add(pair.Value);
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextColored(ImGuiColors.HealerGreen, "Search for a desynthesis source");
        var buttonWidth = ImGui.GetContentRegionAvail().X / 2;
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Button(FontAwesomeIcon.Search.ToIconString(), new Vector2(buttonWidth, 0));
        ImGui.PopFont();

        if (ExcelSheetSelector.ExcelSheetPopup("ItemAddPopup", out var row, SourceOptions))
            SourceSearchResult = row;

        if (SourceSearchResult == 0)
        {

            ImGui.EndTabItem();
            return;
        }

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        var sourceItem = ItemSheet.GetRow(SourceSearchResult)!;
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Searched for {Utils.ToStr(sourceItem.Name)}");
        if (!historyDict.TryGetValue(SourceSearchResult, out var history))
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Nothing found for this item ...");

            ImGui.EndTabItem();
            return;
        }

        var statDict = new Dictionary<uint, (uint Min, uint Max)>();
        foreach (var result in history.SelectMany(h => h.Received).Where(h => h.Item > 0))
        {
            if (!statDict.TryAdd(result.Item, (result.Count, result.Count)))
            {
                var stat = statDict[result.Item];
                if (stat.Min > result.Count)
                    statDict[result.Item] = (result.Count, stat.Max);

                if (stat.Max < result.Count)
                    statDict[result.Item] = (stat.Min, result.Count);
            }
        }

        var desynthesized = history.Count;
        ImGui.TextColored(ImGuiColors.HealerGreen, $"Desynthesized {desynthesized:N0} time{(desynthesized > 1 ? "s" : "")}");
        if (ImGui.BeginTable($"##HistoryStats", 4, 0, new Vector2(300, 0)))
        {
            ImGui.TableSetupColumn("##statItemName", 0, 0.6f);
            ImGui.TableSetupColumn("##statMin", 0, 0.1f);
            ImGui.TableSetupColumn("##statSymbol", 0, 0.05f);
            ImGui.TableSetupColumn("##statMax", 0, 0.1f);

            foreach (var statPair in statDict.OrderByDescending(pair => pair.Key))
            {
                var name = Utils.ToStr(ItemSheet.GetRow(statPair.Key)!.Name);
                ImGui.TableNextColumn();
                if (ImGui.Selectable($"{name}"))
                    ImGui.SetClipboardText(name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{statPair.Value.Min}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted("-");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{statPair.Value.Max}");

                ImGui.TableNextRow();
            }

            ImGui.EndTable();
        }

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextColored(ImGuiColors.HealerGreen, $"History:");
        ImGui.Indent(10.0f);
        if (ImGui.BeginTable($"##HistoryTable", 3))
        {
            ImGui.TableSetupColumn("##icon", 0, 0.2f);
            ImGui.TableSetupColumn("##item");
            ImGui.TableSetupColumn("##amount", 0, 0.2f);

            foreach (var result in history)
            {
                foreach (var itemResult in result.Received)
                {
                    var item = ItemSheet.GetRow(itemResult.Item)!;

                    ImGui.TableNextColumn();
                    DrawIcon(item.Icon);
                    ImGui.TableNextColumn();

                    var name = Utils.ToStr(item.Name);
                    ImGui.TextUnformatted(name);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Utils.ToStr(item.Name));

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"x{itemResult.Count}");
                    ImGui.TableNextRow();
                }

                // add spacing
                ImGui.TableNextColumn();
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.TableNextRow();
            }

            ImGui.EndTable();
        }
        ImGui.Unindent(10.0f);

        ImGui.EndTabItem();
    }

    private void ItemSearch(IEnumerable<CharacterConfiguration> characters)
    {
        if (!ImGui.BeginTabItem("Search Item"))
            return;

        var historyItemDict = new Dictionary<uint, Dictionary<uint, List<DesynthResult>>>();
        foreach (var pair in characters.SelectMany(c => c.Storage.History))
        {
            if (!pair.Value.Received.Any())
            {
                PluginLog.Error($"Found error entry: {pair.Key}");
                continue;
            }

            if (!historyItemDict.TryAdd(pair.Value.Received.First().Item, new Dictionary<uint, List<DesynthResult>> { {pair.Value.Source, new List<DesynthResult> {pair.Value}} }))
                if (!historyItemDict[pair.Value.Received.First().Item].TryAdd(pair.Value.Source, new List<DesynthResult> { pair.Value }))
                    historyItemDict[pair.Value.Received.First().Item][pair.Value.Source].Add(pair.Value);
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextColored(ImGuiColors.HealerGreen, "Search for a desynthesis reward");
        var buttonWidth = ImGui.GetContentRegionAvail().X / 2;
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Button(FontAwesomeIcon.Search.ToIconString(), new Vector2(buttonWidth, 0));
        ImGui.PopFont();

        if (ExcelSheetSelector.ExcelSheetPopup("ItemAddPopup", out var row, ItemOptions))
            ItemSearchResult = row;

        if (ItemSearchResult == 0)
        {
            ImGui.EndTabItem();
            return;
        }

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        var sourceItem = ItemSheet.GetRow(ItemSearchResult)!;
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Searched for {Utils.ToStr(sourceItem.Name)}");
        if (!historyItemDict.TryGetValue(ItemSearchResult, out var history))
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Nothing found for this item ...");

            ImGui.EndTabItem();
            return;
        }

        var statDict = new Dictionary<uint, (uint Min, uint Max)>();
        foreach (var (source, result) in history.Where(pair => pair.Key > 0))
        {
            foreach (var desynthResult in result)
            {
                var count = desynthResult.Received.First().Count;
                if (!statDict.TryAdd(source, (count, count)))
                {
                    var stat = statDict[source];
                    if (stat.Min > count)
                        statDict[source] = (count, stat.Max);

                    if (stat.Max < count)
                        statDict[source] = (stat.Min, count);
                }
            }
        }

        var desynthesized = history.Values.Sum(list => list.Count);
        ImGui.TextColored(ImGuiColors.HealerGreen, $"Seen as reward {desynthesized:N0} time{(desynthesized > 1 ? "s" : "")}");
        if (ImGui.BeginTable($"##HistoryStats", 4, 0, new Vector2(300, 0)))
        {
            ImGui.TableSetupColumn("##statItemName", 0, 0.6f);
            ImGui.TableSetupColumn("##statMin", 0, 0.1f);
            ImGui.TableSetupColumn("##statSymbol", 0, 0.05f);
            ImGui.TableSetupColumn("##statMax", 0, 0.1f);

            foreach (var statPair in statDict.OrderByDescending(pair => pair.Key))
            {
                var name = Utils.ToStr(ItemSheet.GetRow(statPair.Key)!.Name);
                ImGui.TableNextColumn();
                if (ImGui.Selectable($"{name}"))
                    ImGui.SetClipboardText(name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{statPair.Value.Min}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted("-");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{statPair.Value.Max}");

                ImGui.TableNextRow();
            }

            ImGui.EndTable();
        }

        ImGui.EndTabItem();
    }
}
