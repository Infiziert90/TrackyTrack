using System.Collections.Concurrent;
using System.Threading.Tasks;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
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
        FilteredSheet = Plugin.Data.GetExcelSheet<Item>()!.Skip(1).Where(i => i.Name.Payloads.Count > 0).Where(i => i.Desynth > 0)
    };

    private static readonly ExcelSheetSelector.ExcelSheetPopupOptions<Item> ItemOptions = new()
    {
        FormatRow = a => a.RowId switch { _ => $"[#{a.RowId}] {Utils.ToStr(a.Name)}" },
        FilteredSheet = Plugin.Data.GetExcelSheet<Item>()!.Skip(1).Where(i => i.Name.Payloads.Count > 0)
    };

    private void DesynthesisTab()
    {
        using var tabItem = ImRaii.TabItem("Desynthesis");
        if (!tabItem.Success)
            return;

        using var tabBar = ImRaii.TabBar("##DesynthTabBar");
        if (!tabBar.Success)
            return;

        // Sort out any character with 0 desynthesis
        var characters = Plugin.CharacterStorage.Values.Where(c => c.Storage.History.Count > 0).ToArray();
        if (characters.Length == 0)
        {
            Helper.NoDesynthesisData();
            return;
        }

        // Fills history cache if total has changed
        FillHistory(characters);

        DesynthesisStats();

        Local(characters);

        Discover();

        Search();

        CrowdSourcedInfo();
    }

    private void DesynthesisStats()
    {
        using var tabItem = ImRaii.TabItem("Stats");
        if (!tabItem.Success)
            return;

        ImGuiHelpers.ScaledDummy(5.0f);

        if (TaskRunning)
        {
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Rebuilding Cache...");
            return;
        }

        ImGui.TextColored(ImGuiColors.DalamudViolet, "General:");
        using var table = ImRaii.Table("##TotalStatsTable", 3);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##stat", 0, 0.8f);
        ImGui.TableSetupColumn("##name");
        ImGui.TableSetupColumn("##amount");

        using var indent = ImRaii.PushIndent(10.0f);

        ImGui.TableNextColumn();
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

        using (ImRaii.PushIndent(10.0f))
        {
            ImGui.TextColored(ImGuiColors.HealerGreen, "Destroyed");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{Utils.ToStr(item.Name)}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{destroyed.Value:N0}");

            var bestItem = RewardHistory.Where(pair => pair.Key is > 20 and < 100_000).MaxBy(pair => pair.Value);
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
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Gil:");

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        var sum = 0UL;
        foreach (var pair in RewardHistory.Where(pair => Desynth.GilItems.ContainsKey(pair.Key)))
            sum += Desynth.GilItems[pair.Key] * pair.Value;

        using (ImRaii.PushIndent(10.0f))
        {
            ImGui.TextColored(ImGuiColors.HealerGreen, "Pure");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{sum:N0}");
        }
    }

    public void Local(CharacterConfiguration[] characters)
    {
        using var tabItem = ImRaii.TabItem("Local");
        if (!tabItem.Success)
            return;

        using var tabBar = ImRaii.TabBar("DesynthLocalBar");
        if (!tabBar.Success)
            return;

        History(characters);

        Rewards();

        Sources();
    }

    private void History(CharacterConfiguration[] characters)
    {
        using var tabItem = ImRaii.TabItem("History");
        if (!tabItem.Success)
            return;

        var selectedCharacter = SelectedCharacter;
        Helper.ClippedCombo("##existingCharacters", ref selectedCharacter, characters, character => $"{character.CharacterName}@{character.World}");
        if (selectedCharacter != SelectedCharacter)
        {
            SelectedCharacter = selectedCharacter;
            SelectedHistory = 0;
        }

        var selectedChar = characters[SelectedCharacter];
        var selectedHistory = selectedChar.Storage.History.Reverse().ToArray();

        Helper.ClippedCombo("##desynthesisSelection", ref SelectedHistory, selectedHistory, pair => $"{pair.Key}");
        Helper.DrawArrows(ref SelectedHistory, selectedHistory.Length);

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        var resultPair = selectedHistory[SelectedHistory];
        var source = ItemSheet.GetRow(resultPair.Value.Source)!;
        Helper.DrawIcon(source.Icon);
        ImGui.SameLine();

        var sourceName = Utils.ToStr(source.Name);
        using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.HealerGreen))
        {
            if (ImGui.Selectable(sourceName))
            {
                ImGui.SetClipboardText(sourceName);
                SourceSearchResult = source.RowId;
                RewardSearchResult = 0;
            }
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"{sourceName}\nClick to copy and set as reward for search");

        new SimpleTable<ItemResult>("##HistoryTable", resultPair.Value.Received.Where(i => i.Item > 0), withIndent: 10.0f)
            .AddColumn("##icon", 0, 0.2f)
            .AddAction(entry => Helper.DrawIcon(ItemSheet.GetRow(entry.Item)!.Icon))
            .AddColumn("##item")
            .AddAction(entry =>
            {
                var item = ItemSheet.GetRow(entry.Item)!;
                var name = Utils.ToStr(item.Name);
                if (ImGui.Selectable(name))
                {
                    ImGui.SetClipboardText(name);
                    SourceSearchResult = 0;
                    RewardSearchResult = item.RowId;
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{name}\nClick to copy and set as reward for search");
            })
            .AddColumn("##amount", 0, 0.2f)
            .AddAction(entry => ImGui.TextUnformatted($"x{entry.Count}"))
            .Draw();
    }

    private void Rewards()
    {
        using var tabItem = ImRaii.TabItem("Rewards");
        if (!tabItem.Success)
            return;

        ImGuiHelpers.ScaledDummy(5.0f);

        if (TaskRunning)
        {
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Rebuilding Cache...");
            return;
        }

        using var child = ImRaii.Child("RewardsTableChild");
        if (!child.Success)
            return;

        using var table = ImRaii.Table("##RewardsTable", 3);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X + 10.0f);
        ImGui.TableSetupColumn("##item");
        ImGui.TableSetupColumn("##amount", 0, 0.2f);

        var items = RewardHistory.Where(pair => pair.Key is > 0 and < 100_000).OrderBy(pair => pair.Key).ToArray();
        using var indent = ImRaii.PushIndent(10.0f);
        using var clipper = new ListClipper(items.Length, itemHeight: Helper.IconSize.Y * ImGuiHelpers.GlobalScale);
        foreach (var i in clipper.Rows)
        {
            var (itemId, count) = items[i];
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
                ImGui.SetTooltip($"{name}\nClick to copy and set as reward for search");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{count}");

            ImGui.TableNextRow();
        }
    }

    private void Sources()
    {
        using var tabItem = ImRaii.TabItem("Sources");
        if (!tabItem.Success)
            return;

        ImGuiHelpers.ScaledDummy(5.0f);

        if (TaskRunning)
        {
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Rebuilding Cache...");
            return;
        }

        using var child = ImRaii.Child("SourceTableChild");
        if (!child.Success)
            return;

        using var table = ImRaii.Table("##DesynthesisSourceTable", 3);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X + 10.0f);
        ImGui.TableSetupColumn("##item");
        ImGui.TableSetupColumn("##amount", 0, 0.2f);

        var items = SourceHistory.OrderByDescending(pair => pair.Value).ToArray();
        using var indent = ImRaii.PushIndent(10.0f);
        using var clipper = new ListClipper(items.Length, itemHeight: Helper.IconSize.Y * ImGuiHelpers.GlobalScale);
        foreach (var i in clipper.Rows)
        {
            var (itemId, count) = items[i];
            var item = ItemSheet.GetRow(itemId)!;

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
                ImGui.SetTooltip($"{name}\nClick to copy and set as source for search");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{count:N0}");

            ImGui.TableNextRow();
        }
    }

    private void Search()
    {
        using var tabItem = ImRaii.TabItem("Search");
        if (!tabItem.Success)
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
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            ImGui.Button(FontAwesomeIcon.Search.ToIconString(), new Vector2(definedSize * 2, 0));
        }

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
            ImGui.TextColored(ImGuiColors.ParsedOrange, "Nothing found for this source item ...");
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
            if (DataSourceSelection == 1 && LocalSourcesCache != null)
                source = LocalSourcesCache[result.Item];

            var sourceRecord = source.Results.FirstOrDefault(r => r.Item == RewardSearchResult);
            var percentage = (double) sourceRecord.Received / source.Records * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), result.Received, percentage);
        }).OrderByDescending(x => x.Percentage);

        ImGui.TextColored(ImGuiColors.HealerGreen, "Chance for each source:");
        PercentageTable("PercentageRewardTable", sortedList);
    }

    private void Discover()
    {
        using var tabItem = ImRaii.TabItem("Catalogue");
        if (!tabItem.Success)
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

        if (CatalogueCache.Length == 0)
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, "Nothing found for this job and item level ...");
            return;
        }

        ImGui.TextColored(ImGuiColors.HealerGreen, $"Found {CatalogueCache.Length:N0} item{(CatalogueCache.Length > 1 ? "s" : "")}");
        using var child = ImRaii.Child("PossibleItemsChild");
        if (!child.Success)
            return;

        new SimpleTable<Item>("##PossibleItemsTable", CatalogueCache, 0, new Vector2(350 * ImGuiHelpers.GlobalScale, 0))
            .AddColumn("##icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X + 5.0f)
            .AddAction(entry => Helper.DrawIcon(entry.Icon))
            .AddColumn("Name##item")
            .AddAction(entry => Helper.SelectableClipboardText(Utils.ToStr(entry.Name)))
            .AddColumn("Item Level##iLvL", 0, 0.3f)
            .AddAction(entry => Helper.RightAlignedText($"{entry.LevelItem.Row}"))
            .Draw();
    }

    private void CrowdSourcedInfo()
    {
        using var tabItem = ImRaii.TabItem("Info");
        if (!tabItem.Success)
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
                try
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
                        // Old corruption
                        if (pair.Value == 0)
                            continue;

                        if (pair.Key > HighestValidID)
                            continue;

                        if (!RewardHistory.TryAdd(pair.Key, pair.Value))
                            RewardHistory[pair.Key] += pair.Value;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.Error(e, "Error while building cache");
                }

                TaskRunning = false;
            });
        }
    }

    private static void MinMaxTable(string identifier, IEnumerable<Result> results, bool showReceived = false)
    {
        new SimpleTable<Result>(identifier, SortByKeyCustom(results), 0, new Vector2((showReceived ? 400 : 300) * ImGuiHelpers.GlobalScale, 0))
            .AddColumn("Item##ItemName", 0, 0.6f)
            .AddAction(entry => Helper.SelectableClipboardText(Utils.ToStr(ItemSheet.GetRow(entry.Item)?.Name ?? "Invalid Data"), 10.0f))
            .AddColumn("Min##StatMin", 0, 0.1f)
            .AddAction(entry => Helper.RightAlignedText(entry.Min.ToString()))
            .AddColumn("##StatSymbol", 0, 0.05f)
            .AddAction(_ => Helper.CenterText("-"))
            .AddColumn("Max##StatMax", 0, 0.1f)
            .AddAction(entry => ImGui.TextUnformatted(entry.Max.ToString()))
            .AddColumn("Received##StatReceived", 0, 0.3f, showReceived)
            .AddAction(entry => ImGui.TextUnformatted($"x{entry.Received:N0}"), showReceived)
            .Draw();
    }

    private static void PercentageTable(string identifier, IOrderedEnumerable<Utils.SortedEntry> sortedList)
    {
        new SimpleTable<Utils.SortedEntry>(identifier, sortedList)
            .AddColumn("##icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X + 10.0f)
            .AddAction(entry => Helper.DrawIcon(entry.Icon, withIndent: 10.0f))
            .AddColumn("Item##item")
            .AddAction(entry => ImGui.TextUnformatted(entry.Name))
            .AddColumn("Pct##percentage", 0, 0.25f)
            .AddAction(entry => ImGui.TextUnformatted($"{entry.Percentage:F2}%"))
            .Draw();
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
