using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private long LastRefresh;
    private readonly Dictionary<uint, bool> Unlocked = [];

    private void GachaTab()
    {
        using var tabItem = ImRaii.TabItem("Gacha");
        if (!tabItem.Success)
            return;

        using var tabBar = ImRaii.TabBar("##GachaTabBar");
        if (!tabBar.Success)
            return;

        // Refreshes the unlocked dict every 5s
        RefreshUnlocks();

        GachaThreeZero();

        GachaFourZero();

        Sanctuary();
    }

    private void GachaThreeZero()
    {
        using var tabItem = ImRaii.TabItem("Gacha 3.0");
        if (!tabItem.Success)
            return;

        if (!Plugin.Configuration.EnableGachaCoffers)
        {
            Helper.TrackingDisabled("Gacha Coffer tracking has been disabled in the config.");
            return;
        }

        var characters = Plugin.CharacterStorage.Values.ToArray();
        if (characters.Length == 0)
        {
            Helper.NoVentureCofferData();
            return;
        }

        var characterGacha = characters.Where(c => c.GachaThreeZero.Opened > 0).ToList();
        if (characterGacha.Count == 0)
        {
            Helper.NoGachaData("Grand Company 3.0");
            return;
        }

        // fill dict in order
        var dict = new Dictionary<uint, uint>();
        foreach (var item in Data.GachaThreeZero.Content)
            dict.Add(item, 0);

        // fill dict with real values
        foreach (var pair in characterGacha.SelectMany(c => c.GachaThreeZero.Obtained))
            dict[pair.Key] += pair.Value;

        var opened = characterGacha.Select(c => c.GachaThreeZero.Opened).Sum();
        var unsortedList = dict.Where(pair => pair.Value > 0).Select(pair =>
        {
            var item = Sheets.ItemSheet.GetRow(pair.Key);
            var count = pair.Value;
            var percentage = (double) count / opened * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), count, percentage);
        });

        var showUnlock = Plugin.Configuration.ShowUnlockCheckmark;
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        if (showUnlock)
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Unlocked: {dict.Count(pair => Unlocked.TryGetValue(pair.Key, out var unlocked) && unlocked)} out of {Data.GachaThreeZero.Content.Count}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Received From Coffers: {dict.Count(pair => pair.Value > 0)} out of {Data.GachaThreeZero.Content.Count}");
        new SimpleTable<Utils.SortedEntry>("##GachaTable", Utils.SortEntries, ImGuiTableFlags.Sortable, withIndent: 10.0f)
            .EnableSortSpec()
            .AddColumn("##icon", entry => Helper.DrawIcon(entry.Icon), ImGuiTableColumnFlags.NoSort, 0.17f)
            .AddColumn("Item##item", entry => Helper.HoverableText(entry.Name))
            .AddColumn("Num##amount", entry => ImGui.TextUnformatted($"x{entry.Count}"), initWidth: 0.2f)
            .AddColumn("Pct##percentage", entry => ImGui.TextUnformatted($"{entry.Percentage:F2}%"), ImGuiTableColumnFlags.DefaultSort, 0.25f)
            .AddColumn("##unlocked", entry => Helper.DrawUnlockedSymbol(Unlocked.TryGetValue(entry.Id, out var unlocked) && unlocked), ImGuiTableColumnFlags.NoSort, 0.1f, showUnlock)
            .Draw(unsortedList);

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextColored(ImGuiColors.ParsedOrange, "Not Received Yet:");
        new SimpleTable<uint>("##GachaMissingTable", Helper.NoSort, withIndent: 10.0f)
            .AddColumn("##missingItemIcon", entry => Helper.DrawIcon(Sheets.ItemSheet.GetRow(entry).Icon), initWidth: 0.17f)
            .AddColumn("Item##missingItem", entry => Helper.HoverableText(Sheets.ItemSheet.GetRow(entry).Name.ExtractText()))
            .AddColumn("##missingUnlocked", entry => Helper.DrawUnlockedSymbol(Unlocked.TryGetValue(entry, out var unlocked) && unlocked), ImGuiTableColumnFlags.NoSort, 0.1f, showUnlock)
            .Draw(Data.GachaThreeZero.Content.Where(i => dict[i] == 0));
    }

    private void GachaFourZero()
    {
        using var tabItem = ImRaii.TabItem("Gacha 4.0");
        if (!tabItem.Success)
            return;

        if (!Plugin.Configuration.EnableGachaCoffers)
        {
            Helper.TrackingDisabled("Gacha Coffer tracking has been disabled in the config.");
            return;
        }

        var characters = Plugin.CharacterStorage.Values.ToArray();
        if (characters.Length == 0)
        {
            Helper.NoVentureCofferData();
            return;
        }

        var characterGacha = characters.Where(c => c.GachaFourZero.Opened > 0).ToList();
        if (characterGacha.Count == 0)
        {
            Helper.NoGachaData("Grand Company 4.0");
            return;
        }

        // fill dict in order
        var dict = new Dictionary<uint, uint>();
        foreach (var item in Data.GachaFourZero.Content)
            dict.Add(item, 0);

        // fill dict with real values
        foreach (var pair in characterGacha.SelectMany(c => c.GachaFourZero.Obtained))
            dict[pair.Key] += pair.Value;

        var opened = characterGacha.Select(c => c.GachaFourZero.Opened).Sum();
        var unsortedList = dict.Where(pair => pair.Value > 0).Select(pair =>
        {
            var item = Sheets.ItemSheet.GetRow(pair.Key);
            var count = pair.Value;
            var percentage = (double) count / opened * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), count, percentage);
        });

        var showUnlock = Plugin.Configuration.ShowUnlockCheckmark;
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        if (showUnlock)
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Unlocked: {dict.Count(pair => Unlocked.TryGetValue(pair.Key, out var unlocked) && unlocked)} out of {Data.GachaFourZero.Content.Count}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Received From Coffers: {dict.Count(pair => pair.Value > 0)} out of {Data.GachaFourZero.Content.Count}");
        new SimpleTable<Utils.SortedEntry>("##GachaTable", Utils.SortEntries, ImGuiTableFlags.Sortable, withIndent: 10.0f)
            .EnableSortSpec()
            .AddColumn("##icon", entry => Helper.DrawIcon(entry.Icon), ImGuiTableColumnFlags.NoSort, 0.17f)
            .AddColumn("Item##item", entry => Helper.HoverableText(entry.Name))
            .AddColumn("Num##amount", entry => ImGui.TextUnformatted($"x{entry.Count}"), initWidth: 0.2f)
            .AddColumn("Pct##percentage", entry => ImGui.TextUnformatted($"{entry.Percentage:F2}%"), ImGuiTableColumnFlags.DefaultSort, 0.25f)
            .AddColumn("##unlocked", entry => Helper.DrawUnlockedSymbol(Unlocked.TryGetValue(entry.Id, out var unlocked) && unlocked), ImGuiTableColumnFlags.NoSort, 0.1f, showUnlock)
            .Draw(unsortedList);

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextColored(ImGuiColors.ParsedOrange, "Not Received Yet:");
        new SimpleTable<uint>("##GachaMissingTable", Helper.NoSort, withIndent: 10.0f)
            .AddColumn("##missingItemIcon", entry => Helper.DrawIcon(Sheets.ItemSheet.GetRow(entry).Icon), initWidth: 0.17f)
            .AddColumn("Item##missingItem", entry => Helper.HoverableText(Sheets.ItemSheet.GetRow(entry).Name.ExtractText()))
            .AddColumn("##missingUnlocked", entry => Helper.DrawUnlockedSymbol(Unlocked.TryGetValue(entry, out var unlocked) && unlocked), ImGuiTableColumnFlags.NoSort, 0.1f, showUnlock)
            .Draw(Data.GachaFourZero.Content.Where(i => dict[i] == 0));
    }

    private void Sanctuary()
    {
        using var tabItem = ImRaii.TabItem("Sanctuary");
        if (!tabItem.Success)
            return;

        if (!Plugin.Configuration.EnableGachaCoffers)
        {
            Helper.TrackingDisabled("Gacha Coffer tracking has been disabled in the config.");
            return;
        }

        var characters = Plugin.CharacterStorage.Values.ToArray();
        if (characters.Length == 0)
        {
            Helper.NoVentureCofferData();
            return;
        }

        var characterGacha = characters.Where(c => c.GachaSanctuary.Opened > 0).ToList();

        // fill dict in order
        var dict = new Dictionary<uint, uint>();
        foreach (var item in Data.Sanctuary.Content)
            dict.Add(item, 0);

        // fill dict with real values
        foreach (var pair in characterGacha.SelectMany(c => c.GachaSanctuary.Obtained))
            dict[pair.Key] += pair.Value;

        var opened = characterGacha.Select(c => c.GachaSanctuary.Opened).Sum();
        var unsortedList = dict.Where(pair => pair.Value > 0).Select(pair =>
        {
            var item = Sheets.ItemSheet.GetRow(pair.Key);
            var count = pair.Value;
            var percentage = (Data.Sanctuary.MultiRewardItems.Contains(pair.Key) ? count / 5.0 : count ) / opened * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), count, percentage);
        });

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Found: {dict.Count(pair => pair.Value > 0)} out of {Data.Sanctuary.Content.Count} items");
        new SimpleTable<Utils.SortedEntry>("##GachaTable", Utils.SortEntries, ImGuiTableFlags.Sortable, withIndent: 10.0f)
            .EnableSortSpec()
            .AddColumn("##icon", entry => Helper.DrawIcon(entry.Icon), ImGuiTableColumnFlags.NoSort, 0.17f)
            .AddColumn("Item##item", entry => Helper.HoverableText(entry.Name))
            .AddColumn("Num##amount", entry => ImGui.TextUnformatted($"x{entry.Count}"), initWidth: 0.2f)
            .AddColumn("Pct##percentage", entry => ImGui.TextUnformatted($"{entry.Percentage:F2}%"), ImGuiTableColumnFlags.DefaultSort, 0.25f)
            .Draw(unsortedList);

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextColored(ImGuiColors.ParsedOrange, "Not Received Yet:");
        new SimpleTable<uint>("##GachaMissingTable", Helper.NoSort, withIndent: 10.0f)
            .AddColumn("##missingItemIcon", entry => Helper.DrawIcon(Sheets.ItemSheet.GetRow(entry).Icon), initWidth: 0.17f)
            .AddColumn("Item##missingItem", entry => Helper.HoverableText(Sheets.ItemSheet.GetRow(entry).Name.ExtractText()))
            .Draw(Data.Sanctuary.Content.Where(i => dict[i] == 0));
    }

    private void RefreshUnlocks()
    {
        if (Environment.TickCount64 < LastRefresh)
            return;

        LastRefresh = Environment.TickCount64 + 5000; // 5s

        foreach (var item in Data.GachaThreeZero.Content)
            Unlocked[item] = CheckUnlockStatus(item);

        foreach (var item in Data.GachaFourZero.Content)
            Unlocked[item] = CheckUnlockStatus(item);
    }

    private static unsafe bool CheckUnlockStatus(uint id)
    {
        if (!Sheets.ItemSheet.TryGetRow(id, out var item))
            return false;

        if (item.ItemAction.RowId == 0)
            return false;

        var action = item.ItemAction.Value;
        var instance = UIState.Instance();
        return action.Type switch
        {
            1322 => instance->PlayerState.IsMountUnlocked(action.Data[0]),
            853 => instance->IsCompanionUnlocked(action.Data[0]),
            _ => false
        };
    }
}
