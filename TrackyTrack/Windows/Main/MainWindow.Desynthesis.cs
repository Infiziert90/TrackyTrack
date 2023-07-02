using TrackyTrack.Data;
using Lumina.Excel.GeneratedSheets;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private int SelectedCharacter;
    private int SelectedHistory;

    private uint SearchResult;

    private static readonly ExcelSheetSelector.ExcelSheetPopupOptions<Item> ItemPopupOptions = new()
    {
        FormatRow = a => a.RowId switch { _ => $"[#{a.RowId}] {Utils.ToStr(a.Name)}" },
        FilteredSheet = Plugin.Data.GetExcelSheet<Item>()!.Skip(1).Where(i => Utils.ToStr(i.Name) != "").Where(i => i.Desynth > 0)
    };

    private void DesynthesisTab()
    {
        if (ImGui.BeginTabItem("Desynthesis"))
        {
            if (ImGui.BeginTabBar("##DesynthTabBar"))
            {
                History();

                Rewards();

                Source();

                Search();
            }
            ImGui.EndTabBar();

            ImGui.EndTabItem();
        }
    }

    private void History()
    {
        if (!ImGui.BeginTabItem("History"))
            return;

        var existingCharacters = Plugin.CharacterStorage.Values
                                       .Select(character => $"{character.CharacterName}@{character.World}")
                                       .ToArray();

        if (!existingCharacters.Any())
        {
            Helper.NoDesynthesisData();
            ImGui.EndTabItem();
            return;
        }

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
        }

        ImGui.EndTable();
        ImGui.EndTabItem();
    }

    private void Rewards()
    {
        if (!ImGui.BeginTabItem("Rewards"))
            return;

        var characters = Plugin.CharacterStorage.Values.ToArray();

        if (!characters.Any())
        {
            Helper.NoDesynthesisData();
            ImGui.EndTabItem();
            return;
        }

        var dict = new Dictionary<uint, uint>();
        foreach (var pair in characters.SelectMany(c => c.Storage.Total))
        {
            if (!dict.TryAdd(pair.Key, pair.Value))
                dict[pair.Key] += pair.Value;
        }

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Desynthesis: {dict.Count(pair => pair.Key is > 0 and < 1000000)}");
        if (ImGui.BeginTable($"##HistoryTable", 3))
        {
            ImGui.TableSetupColumn("##icon", 0, 0.2f);
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
                ImGui.TextUnformatted(name);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Utils.ToStr(item.Name));

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{count}");
                ImGui.TableNextRow();
            }

            ImGui.Unindent(10.0f);
        }

        ImGui.EndTable();
        ImGui.EndTabItem();
    }

    private void Search()
    {
        if (!ImGui.BeginTabItem("Search"))
            return;

        var characters = Plugin.CharacterStorage.Values.ToArray();

        if (!characters.Any())
        {
            Helper.NoDesynthesisData();
            ImGui.EndTabItem();
            return;
        }

        var historyDict = new Dictionary<uint, List<DesynthResult>>();
        foreach (var pair in characters.SelectMany(c => c.Storage.History))
        {
            if (!historyDict.TryAdd(pair.Value.Source, new List<DesynthResult> {pair.Value}))
                historyDict[pair.Value.Source].Add(pair.Value);
        }

        ImGui.TextColored(ImGuiColors.HealerGreen, "Search for an item");
        var buttonWidth = ImGui.GetContentRegionAvail().X / 2;
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Button(FontAwesomeIcon.Search.ToIconString(), new Vector2(buttonWidth, 0));
        ImGui.PopFont();

        if (ExcelSheetSelector.ExcelSheetPopup("ItemAddPopup", out var row, ItemPopupOptions))
            SearchResult = row;

        if (SearchResult == 0)
            return;

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        var sourceItem = ItemSheet.GetRow(SearchResult)!;
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Searched for {Utils.ToStr(sourceItem.Name)}");
        if (!historyDict.TryGetValue(SearchResult, out var history))
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
        if (ImGui.BeginTable($"##HistoryStats", 4, 0, new Vector2(250, 0)))
        {
            ImGui.TableSetupColumn("##statItemName", 0, 0.6f);
            ImGui.TableSetupColumn("##statMin", 0, 0.1f);
            ImGui.TableSetupColumn("##statSymbol", 0, 0.05f);
            ImGui.TableSetupColumn("##statMax", 0, 0.1f);

            foreach (var statPair in statDict.OrderByDescending(pair => pair.Key))
            {
                var item = ItemSheet.GetRow(statPair.Key)!;
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{Utils.ToStr(item.Name)}");

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

    private void Source()
    {
        if (!ImGui.BeginTabItem("Sources"))
            return;

        var characters = Plugin.CharacterStorage.Values.ToArray();

        if (!characters.Any())
        {
            Helper.NoDesynthesisData();
            ImGui.EndTabItem();
            return;
        }

        var numberOfDesynthesis = new Dictionary<uint, uint>();
        foreach (var pair in characters.SelectMany(c => c.Storage.History))
        {
            if (!numberOfDesynthesis.TryAdd(pair.Value.Source, 1))
                numberOfDesynthesis[pair.Value.Source] += 1;
        }

        ImGui.TextColored(ImGuiColors.HealerGreen, $"Number of Desynthesis:");
        ImGui.Indent(10.0f);
        if (ImGui.BeginTable($"##DesynthesisSourceTable", 3))
        {
            ImGui.TableSetupColumn("##icon", 0, 0.2f);
            ImGui.TableSetupColumn("##item");
            ImGui.TableSetupColumn("##amount", 0, 0.2f);

            foreach (var (source, count) in numberOfDesynthesis.OrderByDescending(pair => pair.Value))
            {
                var item = ItemSheet.GetRow(source)!;

                ImGui.TableNextColumn();
                DrawIcon(item.Icon);
                ImGui.TableNextColumn();

                var name = Utils.ToStr(item.Name);
                ImGui.TextUnformatted(name);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Utils.ToStr(item.Name));

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{count:N0}");
                ImGui.TableNextRow();
            }

            ImGui.EndTable();
        }
        ImGui.Unindent(10.0f);

        ImGui.EndTabItem();
    }
}
