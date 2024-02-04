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
    private int DataSourceSelection;
    private int SearchForSelection;
    private int ILvLSearchResult = 1;

    private int HighestILvL;
    private int SelectedJob;
    private bool ExcludeGear = true;
    private bool ExcludeNonMB = true;

    private Item[] CatalogueCache = null!;

    private bool TaskRunning;
    private int LastHistoryCount;
    private readonly ConcurrentDictionary<uint, uint> SourceHistory = new();
    private readonly ConcurrentDictionary<uint, uint> RewardHistory = new();

    public Dictionary<uint, History>? LocalSourcesCache;
    public Dictionary<uint, History>? LocalRewardsCache;

    public uint LowestValidID;
    public uint HighestValidID;

    public void InitializeDesynth()
    {
        DesynthCache = ItemSheet.Where(i => i.Desynth > 0).ToArray();
        HighestILvL = DesynthCache.Select(i => (int)i.LevelItem.Row).Max();

        LowestValidID = 100;
        HighestValidID = ItemSheet.Where(i => i.Icon != 0).MaxBy(i => i.RowId)!.RowId;

        // Fill once
        CatalogueCache = BuildCatalogue();
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
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Rebuilding Cache...1");

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
        Helper.DrawIcon(source.Icon);
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
                Helper.DrawIcon(item.Icon);
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
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X + 10.0f);
                ImGui.TableSetupColumn("##item");
                ImGui.TableSetupColumn("##amount", 0, 0.2f);

                ImGui.Indent(10.0f);
                foreach (var (itemId, count) in RewardHistory.Where(pair => pair.Key is > 0 and < 1000000).OrderBy(pair => pair.Key))
                {
                    var item = ItemSheet.GetRow(itemId)!;

                    ImGui.TableNextColumn();
                    Helper.DrawIcon(item.Icon);
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
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X + 10.0f);
                ImGui.TableSetupColumn("##item");
                ImGui.TableSetupColumn("##amount", 0, 0.2f);

                ImGui.Indent(10.0f);
                foreach (var (source, count) in SourceHistory.OrderByDescending(pair => pair.Value))
                {
                    var item = ItemSheet.GetRow(source)!;

                    ImGui.TableNextColumn();
                    Helper.DrawIcon(item.Icon);
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

        var longText = "Data Source";
        var width = ImGui.CalcTextSize(longText).X + (20.0f * ImGuiHelpers.GlobalScale);

        var definedSize = ImGui.CalcTextSize("Crowd Sourced").X + (50.0f * ImGuiHelpers.GlobalScale);
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudViolet, longText);
        ImGui.SameLine(width);
        Helper.ToggleButton("Crowd Sourced", "Local", ref DataSourceSelection, definedSize);

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Search For");
        ImGui.SameLine(width);
        Helper.ToggleButton("Source", "Reward", ref SearchForSelection, definedSize);

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Search");
        ImGui.SameLine(width);
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Button($"{FontAwesomeIcon.Search.ToIconString()}##Sources", new Vector2(definedSize * 2, 0));
        ImGui.PopFont();

        if (SearchForSelection == 0)
        {
            if (ExcelSheetSelector.ExcelSheetPopup("SourceResultPopup", out var sourceRow, SourceOptions))
            {
                SourceSearchResult = sourceRow;
                RewardSearchResult = 0;
            }
        }
        else
        {
            if (ExcelSheetSelector.ExcelSheetPopup("ItemResultPopup", out var itemRow, ItemOptions))
            {
                SourceSearchResult = 0;
                RewardSearchResult = itemRow;
            }
        }

        ImGuiHelpers.ScaledDummy(10.0f);

        if (RewardSearchResult > 0)
            RewardSearch();
        else if (SourceSearchResult > 0)
            SourceSearch();

        ImGui.EndTabItem();
    }

    private void SourceSearch()
    {
        var dict = Plugin.Importer.SourcedData.Sources;
        if (DataSourceSelection == 1)
        {
            if (LocalSourcesCache == null)
                FillLocalCache(Plugin.CharacterStorage.Values.SelectMany(h => h.Storage.History.Values));

            dict = LocalSourcesCache!;
        }

        var sourceItem = ItemSheet.GetRow(SourceSearchResult)!;
        Helper.IconHeader(sourceItem.Icon, new Vector2(32, 32), Utils.ToStr(sourceItem.Name), ImGuiColors.ParsedOrange);
        if (!dict.TryGetValue(SourceSearchResult, out var history))
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Nothing found for this source item ...");
            return;
        }

        var desynthesized = history.Records;
        ImGui.TextColored(ImGuiColors.HealerGreen, $"Desynthesized {desynthesized:N0} time{(desynthesized > 1 ? "s" : "")}");
        MinMaxTable("HistoryStats", history.Results, true);

        ImGuiHelpers.ScaledDummy(5.0f);

        var sortedList = history.Results.Where(result => result.Item > 20).Select(result =>
        {
            var item = ItemSheet.GetRow(result.Item) ?? ItemSheet.GetRow(1)!;
            var count = result.Received;
            var percentage = (double) count / desynthesized * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), count, percentage);
        }).OrderByDescending(x => x.Percentage);

        ImGui.TextColored(ImGuiColors.HealerGreen, "Percentages:");
        PercentageTable("PercentageRewardTable", sortedList);
    }

    private void RewardSearch()
    {
        var dict = Plugin.Importer.SourcedData.Rewards;
        if (DataSourceSelection == 1)
        {
            if (LocalRewardsCache == null)
                FillLocalCache(Plugin.CharacterStorage.Values.SelectMany(h => h.Storage.History.Values));

            dict = LocalRewardsCache!;
        }

        var sourceItem = ItemSheet.GetRow(RewardSearchResult)!;
        Helper.IconHeader(sourceItem.Icon, new Vector2(32, 32), Utils.ToStr(sourceItem.Name), ImGuiColors.ParsedOrange);
        if (!dict.TryGetValue(RewardSearchResult, out var history))
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Nothing found for this reward item ...");
            return;
        }

        var desynthesized = history.Records;
        ImGui.TextColored(ImGuiColors.HealerGreen, $"Seen as reward {desynthesized:N0} time{(desynthesized > 1 ? "s" : "")}");
        MinMaxTable("HistoryStats", history.Results);

        ImGuiHelpers.ScaledDummy(5.0f);

        var sortedList = history.Results.Select(result =>
        {
            var item = ItemSheet.GetRow(result.Item) ?? ItemSheet.GetRow(1)!;
            var source = Plugin.Importer.SourcedData.Sources[result.Item];
            var sourceRecord = source.Results.First(r => r.Item == RewardSearchResult);
            var percentage = (double) sourceRecord.Received / source.Records * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), result.Received, percentage);
        }).OrderByDescending(x => x.Percentage);

        ImGui.TextColored(ImGuiColors.HealerGreen, "Chance for each source:");
        PercentageTable("PercentageRewardTable", sortedList);
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
        changed |= ImGui.Checkbox("Exclude MarketBoard Prohibited", ref ExcludeNonMB);

        if (changed)
            CatalogueCache = BuildCatalogue();

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        if (!CatalogueCache.Any())
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Nothing found for this job and item level ...");

            ImGui.EndTabItem();
            return;
        }

        ImGui.TextColored(ImGuiColors.HealerGreen, $"Found {CatalogueCache.Length:N0} item{(CatalogueCache.Length > 1 ? "s" : "")}");
        if (ImGui.BeginChild("##PossibleItemsChild"))
        {
            if (ImGui.BeginTable($"##PossibleItemsTable", 3, 0, new Vector2(350 * ImGuiHelpers.GlobalScale, 0)))
            {
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X + 5.0f);
                ImGui.TableSetupColumn("Name##item");
                ImGui.TableSetupColumn("Item Level##iLvL", 0, 0.3f);

                ImGui.TableHeadersRow();
                foreach (var item in CatalogueCache)
                {
                    ImGui.TableNextColumn();
                    Helper.DrawIcon(item.Icon);
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

    private void FillHistory(IEnumerable<CharacterConfiguration> characters)
    {
        if (TaskRunning)
            return;

        var total = characters.Sum(c => c.Storage.History.Count);
        if (LastHistoryCount != total)
        {
            // We set true outside so that all follow up functions know this is running
            // In rare cases the follow up function can run before the new thread starts to execute
            TaskRunning = true;
            LastHistoryCount = total;
            SourceHistory.Clear();
            RewardHistory.Clear();

            // We reset the local caches here too, but rebuild them later
            LocalSourcesCache = null;
            LocalRewardsCache = null;

            Task.Run(() =>
            {
                var allCharacters = Plugin.CharacterStorage.Values.Where(c => c.Storage.History.Count > 0).ToArray();
                foreach (var pair in allCharacters.SelectMany(c => c.Storage.History))
                {
                    if (pair.Value.Source < LowestValidID || pair.Value.Source > HighestValidID)
                        continue;

                    if (!SourceHistory.TryAdd(pair.Value.Source, 1))
                        SourceHistory[pair.Value.Source] += 1;
                }

                foreach (var pair in allCharacters.SelectMany(c => c.Storage.Total))
                {
                    if (pair.Key < LowestValidID || pair.Key > HighestValidID)
                        continue;

                    if (!RewardHistory.TryAdd(pair.Key, pair.Value))
                        RewardHistory[pair.Key] += pair.Value;
                }

                TaskRunning = false;
            });
        }
    }

    private static void MinMaxTable(string identifier, IEnumerable<Result> results, bool showReceived = false)
    {
        if (ImGui.BeginTable(identifier, showReceived ? 5 : 4, 0, new Vector2((showReceived ? 400 : 300) * ImGuiHelpers.GlobalScale, 0)))
        {
            ImGui.TableSetupColumn("Item##ItemName", 0, 0.6f);
            ImGui.TableSetupColumn("Min##StatMin", 0, 0.1f);
            ImGui.TableSetupColumn("##StatSymbol", 0, 0.05f);
            ImGui.TableSetupColumn("Max##StatMax", 0, 0.1f);
            if (showReceived)
                ImGui.TableSetupColumn("Received##StatReceived", 0, 0.3f);

            ImGui.TableHeadersRow();
            foreach (var result in SortByKeyCustom(results))
            {
                var name = Utils.ToStr(ItemSheet.GetRow(result.Item)?.Name ?? "Invalid Data");
                ImGui.TableNextColumn();
                ImGuiHelpers.ScaledIndent(10.0f);
                if (ImGui.Selectable($"{name}"))
                    ImGui.SetClipboardText(name);
                ImGuiHelpers.ScaledIndent(-10.0f);

                ImGui.TableNextColumn();
                Helper.RightAlignedText(result.Min.ToString());

                ImGui.TableNextColumn();
                Helper.CenterText("-");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(result.Max.ToString());

                if (showReceived)
                {
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"x{result.Received:N0}");
                }

                ImGui.TableNextRow();
            }

            ImGui.EndTable();
        }
    }

    private static void PercentageTable(string identifier, IOrderedEnumerable<Utils.SortedEntry> sortedList)
    {
        if (ImGui.BeginTable(identifier, 3))
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X + 10.0f);
            ImGui.TableSetupColumn("Item##item");
            ImGui.TableSetupColumn("Pct##percentage", 0, 0.25f);

            ImGui.TableHeadersRow();
            foreach (var sortedEntry in sortedList)
            {
                ImGui.TableNextColumn();
                ImGuiHelpers.ScaledIndent(10.0f);
                Helper.DrawIcon(sortedEntry.Icon);
                ImGuiHelpers.ScaledIndent(-10.0f);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(sortedEntry.Name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{sortedEntry.Percentage:F2}%");

                ImGui.TableNextRow();
            }

            ImGui.EndTable();
        }
    }

    public Item[] BuildCatalogue()
    {
        return DesynthCache.Where(i => i.Desynth > 0)
                              .Where(i => i.ItemUICategory.Row != 39)
                              .Where(i => i.LevelItem.Row > ILvLSearchResult)
                              .Where(i => i.ClassJobRepair.Row == SelectedJob + 8)
                              .Where(i => !ExcludeGear || i.EquipSlotCategory.Row == 0)
                              .Where(i => !ExcludeNonMB || !i.IsUntradable)
                              .OrderBy(i => i.LevelItem.Row)
                              .ToArray();
    }

    private const int GilItemOrder = 1_000_000;
    private const int CrystalOrder = 2_000_000;
    public static IOrderedEnumerable<Result> SortByKeyCustom(IEnumerable<Result> unsortedArray)
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

    public void FillLocalCache(IEnumerable<DesynthResult> results)
    {
        LocalSourcesCache = new Dictionary<uint, History>();
        LocalRewardsCache = new Dictionary<uint, History>();

        var records = new Dictionary<uint, uint>();
        var final = new Dictionary<uint, Dictionary<uint, Importer.Stats>>();
        foreach (var result in results)
        {
            if (result.Source > HighestValidID || result.Source < LowestValidID)
                continue;

            if (!records.TryAdd(result.Source, 1))
                records[result.Source]++;

            if (result.Received.Length > 3)
                continue;

            foreach (var received in result.Received)
            {
                var item = received.Item;
                var amount = received.Count;

                switch (item)
                {
                    case < 100:
                        continue;
                    case > 100_000:
                        continue;
                }

                final.TryAdd(result.Source, new Dictionary<uint, Importer.Stats>());

                var t = final[result.Source];
                if (!t.TryAdd(item, new Importer.Stats(amount, amount)))
                {
                    var minMax = t[item];
                    minMax.Records++;
                    minMax.Min = Math.Min(amount, minMax.Min);
                    minMax.Max = Math.Max(amount, minMax.Max);
                    t[item] = minMax;
                }
            }
        }

        foreach (var (source, rewards) in final)
        {
            var r = new List<Result>();
            foreach (var (reward, minMax) in rewards)
            {
                r.Add(new Result(reward, minMax.Min, minMax.Max, minMax.Records));

                if (!LocalRewardsCache.TryAdd(reward, new History { Records = minMax.Records, Results = new [] { new Result(source, minMax.Min, minMax.Max, minMax.Records) } }))
                {
                    var h = LocalRewardsCache[reward];
                    h.Records += minMax.Records;
                    h.Results = h.Results.Append(new Result(source, minMax.Min, minMax.Max, minMax.Records)).ToArray();
                    LocalRewardsCache[reward] = h;
                }
            }

            LocalSourcesCache.Add(source, new History {
                Records = records[source],
                Results = r.ToArray()
            });
        }
    }
}
