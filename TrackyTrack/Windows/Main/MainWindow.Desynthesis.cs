using System.Collections.Concurrent;
using System.Threading.Tasks;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using TrackyTrack.Data;
using Lumina.Excel.Sheets;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private static readonly string[] Jobs = ["CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL"];

    private int SelectedCharacter;
    private int SelectedHistory;

    private int ILvLSearchResult = 1;

    private int SelectedJob;
    private bool ExcludeGear = true;
    private bool ExcludeNonMB = true;

    private Item[] CatalogueCache = null!;

    private bool TaskRunning;
    private int LastHistoryCount;
    private readonly ConcurrentDictionary<uint, uint> SourceHistory = new();
    private readonly ConcurrentDictionary<uint, uint> RewardHistory = new();

    private void InitializeDesynth()
    {
        // Fill once
        CatalogueCache = BuildCatalogue();
    }

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

        ImGui.TableSetupColumn("##stat", ImGuiTableColumnFlags.WidthStretch, 0.8f);
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
        var item = Sheets.GetItem(destroyed.Key);

        using (ImRaii.PushIndent(10.0f))
        {
            ImGui.TextColored(ImGuiColors.HealerGreen, "Destroyed");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(item.Name.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{destroyed.Value:N0}");

            var bestItem = RewardHistory.Where(pair => pair.Key is > 20 and < 100_000).MaxBy(pair => pair.Value);
            item = Sheets.GetItem(bestItem.Key);
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Rewarded");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(item.Name.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{bestItem.Value:N0}");

            var bestCrystal = RewardHistory.Where(pair => pair.Key is > 0 and < 20).MaxBy(pair => pair.Value);
            item = Sheets.GetItem(bestCrystal.Key);
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Crystal");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(item.Name.ToString());
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

    private void Local(CharacterConfiguration[] characters)
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
        var source = Sheets.GetItem(resultPair.Value.Source);
        Helper.DrawIcon(source.Icon);
        ImGui.SameLine();

        var sourceName = source.Name.ToString();
        using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.HealerGreen))
        {
            if (ImGui.Selectable(sourceName))
                ImGui.SetClipboardText(sourceName);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"{sourceName}\nClick to copy");

        new SimpleTable<ItemResult>("##HistoryTable", Helper.NoSort, withIndent: 10.0f)
            .HideHeaderRow()
            .AddIconColumn("##icon", entry => Helper.DrawIcon(entry.ToItemRow().Icon))
            .AddColumn("##item", entry =>
            {
                var item = entry.ToItemRow();
                var name = item.Name.ToString();
                if (ImGui.Selectable(name))
                    ImGui.SetClipboardText(name);

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{name}\nClick to copy");
            })
            .AddColumn("##amount", entry => ImGui.TextUnformatted($"x{entry.Count}"), ImGuiTableColumnFlags.WidthStretch, initWidth: 0.2f)
            .Draw(resultPair.Value.Received.Where(i => i.Item > 0));
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
        ImGui.TableSetupColumn("##amount", ImGuiTableColumnFlags.WidthStretch, 0.2f);

        var items = RewardHistory.Where(pair => pair.Key is > 0 and < 100_000).OrderBy(pair => pair.Key).ToArray();
        using var indent = ImRaii.PushIndent(10.0f);
        using var clipper = new ListClipper(items.Length, itemHeight: Helper.IconSize.Y * ImGuiHelpers.GlobalScale);
        foreach (var i in clipper.Rows)
        {
            var (itemId, count) = items[i];
            var item = Sheets.GetItem(itemId);

            ImGui.TableNextColumn();
            Helper.DrawIcon(item.Icon);

            ImGui.TableNextColumn();
            var name = item.Name.ToString();
            if (ImGui.Selectable(name))
                ImGui.SetClipboardText(name);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{name}\nClick to copy");

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
        ImGui.TableSetupColumn("##amount", ImGuiTableColumnFlags.WidthStretch, 0.2f);

        var items = SourceHistory.OrderByDescending(pair => pair.Value).ToArray();
        using var indent = ImRaii.PushIndent(10.0f);
        using var clipper = new ListClipper(items.Length, itemHeight: Helper.IconSize.Y * ImGuiHelpers.GlobalScale);
        foreach (var i in clipper.Rows)
        {
            var (itemId, count) = items[i];
            var item = Sheets.GetItem(itemId);

            ImGui.TableNextColumn();
            Helper.DrawIcon(item.Icon);

            ImGui.TableNextColumn();
            var name = item.Name.ToString();
            if (ImGui.Selectable(name))
                ImGui.SetClipboardText(name);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{name}\nClick to copy");

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

        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.ParsedBlue))
        {
            if (ImGui.Button("Improved Website Version"))
                Dalamud.Utility.Util.OpenLink("https://xivstats.com/desynth");
        }
    }

    private void Discover()
    {
        using var tabItem = ImRaii.TabItem("Catalogue");
        if (!tabItem.Success)
            return;

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextColored(ImGuiColors.HealerGreen, "Search for a desynthesizable source");

        var changed = false;
        if (ImGui.SliderInt("##ilvlInput", ref ILvLSearchResult, 1, Sheets.HighestILvl, "Item Level %d"))
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

        new SimpleTable<Item>("##PossibleItemsTable", Helper.NoSort)
            .AddIconColumn("##icon", entry => Helper.DrawIcon(entry.Icon))
            .AddColumn("Name##item", entry => Helper.SelectableClipboardText(entry.Name.ToString()))
            .AddColumn("Item Level##iLvL", entry => Helper.RightAlignedText($"{entry.LevelItem.RowId}"), ImGuiTableColumnFlags.WidthStretch, initWidth: 0.3f)
            .Draw(CatalogueCache);
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

            Task.Run(() =>
            {
                try
                {
                    var allCharacters = Plugin.CharacterStorage.Values.Where(c => c.Storage.History.Count > 0).ToArray();
                    foreach (var pair in allCharacters.SelectMany(c => c.Storage.History))
                    {
                        if (pair.Value.Source < Sheets.LowewstValidId || pair.Value.Source > Sheets.HighestValidId)
                            continue;

                        if (!SourceHistory.TryAdd(pair.Value.Source, 1))
                            SourceHistory[pair.Value.Source] += 1;
                    }

                    foreach (var pair in allCharacters.SelectMany(c => c.Storage.Total))
                    {
                        // Old corruption
                        if (pair.Value == 0)
                            continue;

                        if (pair.Key > Sheets.HighestValidId)
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

    private Item[] BuildCatalogue()
    {
        return Sheets.DesynthCache
              .Where(i => i.Desynth > 0)
              .Where(i => i.ItemUICategory.RowId != 39)
              .Where(i => i.LevelItem.RowId > ILvLSearchResult)
              .Where(i => i.ClassJobRepair.RowId == SelectedJob + 8)
              .Where(i => !ExcludeGear || i.EquipSlotCategory.RowId == 0)
              .Where(i => !ExcludeNonMB || !i.IsUntradable)
              .OrderBy(i => i.LevelItem.RowId)
              .ToArray();
    }
}
