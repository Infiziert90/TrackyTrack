using System.Collections.Concurrent;
using System.Threading.Tasks;
using Dalamud.Interface.Utility;
using TrackyTrack.Data;
using Lumina.Excel.GeneratedSheets;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private static readonly string[] Jobs = { "CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL" };

    private Item[] DesynthCache = null!;

    private int SelectedCharacter;
    private int SelectedHistory;

    private uint SourceSearchResult;
    private uint RewardSearchResult;
    private int ILvLSearchResult = 1;

    private int HighestILvL;
    private int SelectedJob;
    private bool ExcludeGear = true;
    private bool ExcludeNonMB = true;

    private Item[] SearchCache = null!;

    private bool TaskRunning;
    private int LastHistoryCount;
    private ConcurrentDictionary<uint, uint> SourceHistory = new();
    private ConcurrentDictionary<uint, uint> RewardHistory = new();

    public void InitializeDesynth()
    {
        DesynthCache = ItemSheet.Where(i => i.Desynth > 0).ToArray();
        HighestILvL = DesynthCache.Select(i => (int)i.LevelItem.Row).Max();

        // Fill once
        SearchCache = DesynthCache.Where(i => i.Desynth > 0)
                                  .Where(i => i.ItemUICategory.Row != 39)
                                  .Where(i => i.LevelItem.Row > ILvLSearchResult)
                                  .Where(i => i.ClassJobRepair.Row == SelectedJob + 8)
                                  .Where(i => !ExcludeGear || i.EquipSlotCategory.Row == 0)
                                  .Where(i => !ExcludeNonMB || !i.IsUntradable)
                                  .OrderBy(i => i.LevelItem.Row)
                                  .ToArray();
    }

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
            // Sort out any character with 0 desynthesis
            var characters = Plugin.CharacterStorage.Values.Where(c => c.Storage.History.Count > 0).ToArray();
            if (!characters.Any())
            {
                Helper.NoDesynthesisData();

                ImGui.EndTabItem();
                return;
            }

            // Fill history if needed
            FillHistory(characters);

            if (ImGui.BeginTabBar("DesynthTabBar"))
            {
                DesynthesisStats();

                Local(characters);

                Discover();

                Search();

                CrowdSourcedInfo();

                ImGui.EndTabBar();
            }
            ImGui.EndTabItem();
        }
    }

    private void DesynthesisStats()
    {
        if (!ImGui.BeginTabItem("Stats##Desynthesis"))
            return;

        ImGuiHelpers.ScaledDummy(5.0f);

        if (TaskRunning)
        {
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Rebuilding Cache...");

            ImGui.EndTabItem();
            return;
        }

        ImGui.TextColored(ImGuiColors.DalamudViolet, "General:");
        if (ImGui.BeginTable($"##TotalStatsTable", 3))
        {
            ImGui.TableSetupColumn("##stat", 0, 0.8f);
            ImGui.TableSetupColumn("##name");
            ImGui.TableSetupColumn("##amount");

            ImGui.TableNextColumn();
            ImGui.Indent(10.0f);
            ImGui.TextColored(ImGuiColors.HealerGreen, "Desynthesized");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{LastHistoryCount:N0} time{(LastHistoryCount > 1 ? "s" : "")}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Most often:");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var destroyed = SourceHistory.MaxBy(pair => pair.Value);
            var item = ItemSheet.GetRow(destroyed.Key)!;

            ImGui.Indent(10.0f);
            ImGui.TextColored(ImGuiColors.HealerGreen, "Destroyed");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{Utils.ToStr(item.Name)}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{destroyed.Value:N0}");

            var bestItem = RewardHistory.Where(pair => pair.Key is > 20 and < 1000000).MaxBy(pair => pair.Value);
            item = ItemSheet.GetRow(bestItem.Key)!;
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Rewarded");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{Utils.ToStr(item.Name)}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{bestItem.Value:N0}");

            var bestCrystal = RewardHistory.Where(pair => pair.Key is > 0 and < 20).MaxBy(pair => pair.Value);
            item = ItemSheet.GetRow(bestCrystal.Key)!;
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Crystal");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{Utils.ToStr(item.Name)}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{bestCrystal.Value:N0}");
            ImGui.Unindent(10.0f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Gil:");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var sum = 0UL;
            foreach (var pair in RewardHistory.Where(pair => Desynth.GilItems.ContainsKey(pair.Key)))
                sum += Desynth.GilItems[pair.Key] * pair.Value;

            ImGui.Indent(10.0f);
            ImGui.TextColored(ImGuiColors.HealerGreen, "Pure");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{sum:N0}");
            ImGui.Unindent(10.0f);

            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }

    public void Local(CharacterConfiguration[] characters)
    {
        if (!ImGui.BeginTabItem("Local##Desynthesis"))
            return;

        if (ImGui.BeginTabBar("DesynthLocalBar"))
        {
            History(characters);

            Rewards();

            Sources();

            ImGui.EndTabBar();
        }

        ImGui.EndTabItem();
    }

    private void History(CharacterConfiguration[] characters)
    {
        if (!ImGui.BeginTabItem("History##Desynthesis"))
            return;

        var existingCharacters = characters.Select(character => $"{character.CharacterName}@{character.World}").ToArray();

        var selectedCharacter = SelectedCharacter;
        ImGui.Combo("##existingCharacters", ref selectedCharacter, existingCharacters, existingCharacters.Length);
        if (selectedCharacter != SelectedCharacter)
        {
            SelectedCharacter = selectedCharacter;
            SelectedHistory = 0;
        }

        var selectedChar = characters[SelectedCharacter];
        var history = selectedChar.Storage.History.Reverse().Select(pair => $"{pair.Key}").ToArray();

        ImGui.Combo("##voyageSelection", ref SelectedHistory, history, history.Length);
        Helper.DrawArrows(ref SelectedHistory, history.Length);

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        var resultPair = selectedChar.Storage.History.Reverse().ToArray()[SelectedHistory];

        var source = ItemSheet.GetRow(resultPair.Value.Source)!;
        DrawIcon(source.Icon);
        ImGui.SameLine();

        var sourceName = Utils.ToStr(source.Name);
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
        if (ImGui.Selectable(sourceName))
        {
            ImGui.SetClipboardText(sourceName);
            SourceSearchResult = source.RowId;
            RewardSearchResult = 0;
        }
        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"{sourceName}\nClick to copy and set as reward for search");

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
                if (ImGui.Selectable(name))
                {
                    ImGui.SetClipboardText(name);
                    SourceSearchResult = 0;
                    RewardSearchResult = item.RowId;
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{Utils.ToStr(item.Name)}\nClick to copy and set as reward for search");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{result.Count}");
                ImGui.TableNextRow();
            }

            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }

    private void Rewards()
    {
        if (!ImGui.BeginTabItem("Rewards##Desynthesis"))
            return;

        ImGuiHelpers.ScaledDummy(5.0f);

        if (TaskRunning)
        {
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Rebuilding Cache...");

            ImGui.EndTabItem();
            return;
        }

        if (ImGui.BeginChild("RewardsTableChild"))
        {
            if (ImGui.BeginTable($"##RewardsTable", 3))
            {
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, IconSize.X + 10.0f);
                ImGui.TableSetupColumn("##item");
                ImGui.TableSetupColumn("##amount", 0, 0.2f);

                ImGui.Indent(10.0f);
                foreach (var (itemId, count) in RewardHistory.Where(pair => pair.Key is > 0 and < 1000000).OrderBy(pair => pair.Key))
                {
                    var item = ItemSheet.GetRow(itemId)!;

                    ImGui.TableNextColumn();
                    DrawIcon(item.Icon);
                    ImGui.TableNextColumn();

                    var name = Utils.ToStr(item.Name);
                    if (ImGui.Selectable(name))
                    {
                        ImGui.SetClipboardText(name);
                        RewardSearchResult = item.RowId;
                        SourceSearchResult = 0;
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"{Utils.ToStr(item.Name)}\nClick to copy and set as reward for search");

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

    private void Sources()
    {
        if (!ImGui.BeginTabItem("Sources##Desynthesis"))
            return;

        ImGuiHelpers.ScaledDummy(5.0f);

        if (TaskRunning)
        {
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Rebuilding Cache...");

            ImGui.EndTabItem();
            return;
        }

        if (ImGui.BeginChild("SourceTableChild"))
        {
            if (ImGui.BeginTable($"##DesynthesisSourceTable", 3))
            {
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, IconSize.X + 10.0f);
                ImGui.TableSetupColumn("##item");
                ImGui.TableSetupColumn("##amount", 0, 0.2f);

                ImGui.Indent(10.0f);
                foreach (var (source, count) in SourceHistory.OrderByDescending(pair => pair.Value))
                {
                    var item = ItemSheet.GetRow(source)!;

                    ImGui.TableNextColumn();
                    DrawIcon(item.Icon);
                    ImGui.TableNextColumn();

                    var name = Utils.ToStr(item.Name);
                    if (ImGui.Selectable(name))
                    {
                        ImGui.SetClipboardText(name);
                        SourceSearchResult = item.RowId;
                        RewardSearchResult = 0;
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"{Utils.ToStr(item.Name)}\nClick to copy and set as source for search");

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

    private void Search()
    {
        if (!ImGui.BeginTabItem("Search##Desynthesis"))
            return;

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Search through the crowd sourced history");
        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.Columns(2);
        var buttonWidth = ImGui.GetContentRegionAvail().X - (20.0f * ImGuiHelpers.GlobalScale);

        ImGui.TextColored(ImGuiColors.HealerGreen, "Source Search");
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Button($"{FontAwesomeIcon.Search.ToIconString()}##sources", new Vector2(buttonWidth, 0));
        ImGui.PopFont();

        if (ExcelSheetSelector.ExcelSheetPopup("SourceResultPopup", out var sourceRow, SourceOptions))
        {
            SourceSearchResult = sourceRow;
            RewardSearchResult = 0;
        }

        ImGui.NextColumn();

        ImGui.TextColored(ImGuiColors.HealerGreen, "Reward Search");
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Button($"{FontAwesomeIcon.Search.ToIconString()}##item", new Vector2(buttonWidth, 0));
        ImGui.PopFont();

        if (ExcelSheetSelector.ExcelSheetPopup("ItemResultPopup", out var itemRow, ItemOptions))
        {
            SourceSearchResult = 0;
            RewardSearchResult = itemRow;
        }

        ImGui.Columns(1);

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        if (RewardSearchResult > 0)
            ItemSearch();
        else if (SourceSearchResult > 0)
            SourceSearch();

        ImGui.EndTabItem();
    }

    private void SourceSearch()
    {
        var sourceItem = ItemSheet.GetRow(SourceSearchResult)!;
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Searched for {Utils.ToStr(sourceItem.Name)}");
        if (!Plugin.Importer.SourcedData.Sources.TryGetValue(SourceSearchResult, out var history))
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Nothing found for this source item ...");
            return;
        }

        var desynthesized = history.Records;
        ImGui.TextColored(ImGuiColors.HealerGreen, $"Desynthesized {desynthesized:N0} time{(desynthesized > 1 ? "s" : "")}");
        if (ImGui.BeginTable($"##HistoryStats", 5, 0, new Vector2(400 * ImGuiHelpers.GlobalScale, 0)))
        {
            ImGui.TableSetupColumn("Reward##statItemName", 0, 0.6f);
            ImGui.TableSetupColumn("Min##statMin", 0, 0.1f);
            ImGui.TableSetupColumn("##statSymbol", 0, 0.05f);
            ImGui.TableSetupColumn("Max##statMax", 0, 0.1f);
            ImGui.TableSetupColumn("Received##received", 0, 0.3f);

            ImGui.TableHeadersRow();

            ImGui.Indent(10.0f);
            foreach (var result in SortByKeyCustom(history.Results))
            {
                var name = Utils.ToStr(ItemSheet.GetRow(result.Item)!.Name);
                ImGui.TableNextColumn();
                if (ImGui.Selectable($"{name}"))
                    ImGui.SetClipboardText(name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{result.Min}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted("-");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{result.Max}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{result.Received:N0}");

                ImGui.TableNextRow();
            }
            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        var sortedList = history.Results.Where(result => result.Item > 20).Select(result =>
        {
            var item = ItemSheet.GetRow(result.Item)!;
            var count = result.Received;
            var percentage = (double) count / desynthesized * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), count, percentage);
        }).OrderByDescending(x => x.Percentage);

        ImGui.TextColored(ImGuiColors.HealerGreen, $"Percentages:");

        if (ImGui.BeginTable($"##PercentageSourceTable", 3))
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, IconSize.X + 10.0f);
            ImGui.TableSetupColumn("Item##item");
            ImGui.TableSetupColumn("Pct##percentage", 0, 0.25f);

            ImGui.Indent(10.0f);
            foreach (var sortedEntry in sortedList)
            {
                ImGui.TableNextColumn();
                DrawIcon(sortedEntry.Icon);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(sortedEntry.Name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{sortedEntry.Percentage:F2}%");

                ImGui.TableNextRow();
            }
            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }
    }

    private void ItemSearch()
    {
        var sourceItem = ItemSheet.GetRow(RewardSearchResult)!;
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Searched for {Utils.ToStr(sourceItem.Name)}");
        if (!Plugin.Importer.SourcedData.Rewards.TryGetValue(RewardSearchResult, out var history))
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Nothing found for this reward item ...");
            return;
        }

        var desynthesized = history.Records;
        ImGui.TextColored(ImGuiColors.HealerGreen, $"Seen as reward {desynthesized:N0} time{(desynthesized > 1 ? "s" : "")}");
        if (ImGui.BeginTable($"##HistoryStats", 4, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0)))
        {
            ImGui.TableSetupColumn("##statItemName", 0, 0.6f);
            ImGui.TableSetupColumn("##statMin", 0, 0.1f);
            ImGui.TableSetupColumn("##statSymbol", 0, 0.05f);
            ImGui.TableSetupColumn("##statMax", 0, 0.1f);

            foreach (var result in SortByKeyCustom(history.Results))
            {
                var name = Utils.ToStr(ItemSheet.GetRow(result.Item)!.Name);
                ImGui.TableNextColumn();
                if (ImGui.Selectable($"{name}"))
                    ImGui.SetClipboardText(name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{result.Min}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted("-");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{result.Max}");

                ImGui.TableNextRow();
            }

            ImGui.EndTable();
        }
    }

    private void Discover()
    {
        if (!ImGui.BeginTabItem("Catalogue##Desynthesis"))
            return;

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextColored(ImGuiColors.HealerGreen, "Search for a desynthesizable source");

        var changed = false;
        if (ImGui.SliderInt("##ilvlInput", ref ILvLSearchResult, 1, HighestILvL, "Item Level %d"))
        {
            ILvLSearchResult = (int) Math.Round(ILvLSearchResult / 5.0) * 5;
            changed = true;
        }
        changed |= ImGui.Combo("##jobSelection", ref SelectedJob, Jobs, Jobs.Length);
        changed |= Helper.DrawArrows(ref SelectedJob, Jobs.Length);
        changed |= ImGui.Checkbox("Exclude Gear", ref ExcludeGear);
        changed |= ImGui.Checkbox("Exclude Marketboard Prohibited", ref ExcludeNonMB);

        if (changed)
        {
            SearchCache = DesynthCache.Where(i => i.ItemUICategory.Row != 39)
                                      .Where(i => i.LevelItem.Row > ILvLSearchResult)
                                      .Where(i => i.ClassJobRepair.Row == SelectedJob + 8)
                                      .Where(i => !ExcludeGear || i.EquipSlotCategory.Row == 0)
                                      .Where(i => !ExcludeNonMB || !i.IsUntradable)
                                      .OrderBy(i => i.LevelItem.Row)
                                      .ToArray();
        }

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        if (!SearchCache.Any())
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Nothing found for this job and item level ...");

            ImGui.EndTabItem();
            return;
        }

        ImGui.TextColored(ImGuiColors.HealerGreen, $"Found {SearchCache.Length:N0} item{(SearchCache.Length > 1 ? "s" : "")}");
        if (ImGui.BeginChild("##PossibleItemsChild"))
        {
            if (ImGui.BeginTable($"##PossibleItemsTable", 3, 0, new Vector2(350 * ImGuiHelpers.GlobalScale, 0)))
            {
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, IconSize.X + 5.0f);
                ImGui.TableSetupColumn("Name##item");
                ImGui.TableSetupColumn("Item Level##iLvL", 0, 0.3f);

                ImGui.TableHeadersRow();
                foreach (var item in SearchCache)
                {
                    ImGui.TableNextColumn();
                    DrawIcon(item.Icon);
                    ImGui.TableNextColumn();

                    var name = Utils.ToStr(item.Name);
                    if (ImGui.Selectable(name))
                        ImGui.SetClipboardText(name);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Click to copy");

                    ImGui.TableNextColumn();
                    Helper.RightAlignedText($"{item.LevelItem.Row}");
                    ImGui.TableNextRow();
                }

                ImGui.EndTable();
            }
        }
        ImGui.EndChild();

        ImGui.EndTabItem();
    }

    private void CrowdSourcedInfo()
    {
        if (!ImGui.BeginTabItem("Info##Desynthesis"))
            return;

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextColored(ImGuiColors.HealerGreen, $"Total Records: {Plugin.Importer.SourcedData.TotalRecords:N0}");
        ImGui.TextColored(ImGuiColors.HealerGreen, $"Last Time Updated: {Plugin.Importer.SourcedData.LastUpdate:dd MMM yyyy hh:mm tt}");

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        Helper.WrappedText(ImGuiColors.DalamudViolet, "All crowd sourced data is provided by the community.");
        Helper.WrappedText(ImGuiColors.DalamudViolet, "To contribute data, allow this plugin to upload data.");
        ImGuiHelpers.ScaledDummy(5.0f);
        Helper.WrappedText(ImGuiColors.DalamudViolet, "Please report any inconsistency.");
        Helper.WrappedText(ImGuiColors.DalamudViolet, "Contact info can be found in the about tab.");

        ImGui.EndTabItem();
    }

    private void FillHistory(CharacterConfiguration[] characters)
    {
        if (TaskRunning)
            return;


        var totalHistory = characters.SelectMany(c => c.Storage.History).ToArray();
        if (LastHistoryCount != totalHistory.Length)
        {
            // We set true outside so that all follow up functions know that the function is currently running
            // In rare cases the follow up function can run before the new thread is executed
            TaskRunning = true;

            Task.Run(() => {
                LastHistoryCount = totalHistory.Length;
                SourceHistory.Clear();
                RewardHistory.Clear();

                foreach (var pair in totalHistory)
                    if (!SourceHistory.TryAdd(pair.Value.Source, 1))
                        SourceHistory[pair.Value.Source] += 1;

                foreach (var pair in characters.SelectMany(c => c.Storage.Total))
                    if (!RewardHistory.TryAdd(pair.Key, pair.Value))
                        RewardHistory[pair.Key] += pair.Value;

                TaskRunning = false;
            });
        }
    }

    private const int GilItemOrder = 1_000_000;
    private const int CrystalOrder = 2_000_000;
    public static IOrderedEnumerable<Result> SortByKeyCustom(Result[] unsortedArray)
    {
        return unsortedArray.OrderBy(result =>
        {
            var idx = result.Item;
            if (idx < 20)
                idx += CrystalOrder;
            else if (Desynth.GilItems.ContainsKey(idx))
                idx += GilItemOrder;

            return idx;
        });
    }
}
