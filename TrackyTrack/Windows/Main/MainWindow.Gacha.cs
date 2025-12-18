using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private long LastRefresh;
    private const int RefreshRate = 5000; // 5s

    private readonly Dictionary<uint, bool> Unlocked = [];

    private Tabs SelectedGachaTab = Tabs.Gacha3;
    private static readonly Tabs[] GachaTabs = [Tabs.Gacha3, Tabs.Gacha4, Tabs.Sanctuary];

    private void GachaTab()
    {
        using var tabItem = ImRaii.TabItem("Gacha");
        if (!tabItem.Success)
            return;

        // Refreshes the unlocked dict every 5s
        RefreshUnlocks();

        var pos = ImGui.GetCursorPos();

        var nameDict = TabHelper.TabSize(GachaTabs);
        var childSize = new Vector2(nameDict.Select(pair => pair.Value.Width).Max(), 0);
        using (var tabChild = ImRaii.Child("Tabs", childSize, true))
        {
            if (tabChild.Success)
            {
                foreach (var (id, (name, _)) in nameDict)
                    if (ImGui.Selectable(name, SelectedGachaTab == id))
                        SelectedGachaTab = id;
            }
        }

        ImGui.SetCursorPos(pos with {X = pos.X + childSize.X});
        using (var contentChild = ImRaii.Child("Content", Vector2.Zero, true))
        {
            if (contentChild.Success)
            {
                switch (SelectedGachaTab)
                {
                    case Tabs.Gacha3:
                        GachaThreeZero();
                        break;
                    case Tabs.Gacha4:
                        GachaFourZero();
                        break;
                    case Tabs.Sanctuary:
                        Sanctuary();
                        break;
                }
            }
        }
    }

    private void GachaThreeZero()
    {
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
        var unsortedList = Utils.ToSortedEntry(dict, opened);

        var showUnlock = Plugin.Configuration.ShowUnlockCheckmark;
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        if (showUnlock)
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Unlocked: {dict.Count(pair => Unlocked.TryGetValue(pair.Key, out var unlocked) && unlocked)} out of {Data.GachaThreeZero.Content.Count}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Received From Coffers: {dict.Count(pair => pair.Value > 0)} out of {Data.GachaThreeZero.Content.Count}");
        DrawTable(unsortedList, showUnlock);

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextColored(ImGuiColors.ParsedOrange, "Not Received:");
        DrawMissingTable(Data.GachaThreeZero.Content.Where(i => dict[i] == 0), showUnlock);
    }

    private void GachaFourZero()
    {
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
        var unsortedList = Utils.ToSortedEntry(dict, opened);

        var showUnlock = Plugin.Configuration.ShowUnlockCheckmark;
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        if (showUnlock)
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Unlocked: {dict.Count(pair => Unlocked.TryGetValue(pair.Key, out var unlocked) && unlocked)} out of {Data.GachaFourZero.Content.Count}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Received From Coffers: {dict.Count(pair => pair.Value > 0)} out of {Data.GachaFourZero.Content.Count}");
        DrawTable(unsortedList, showUnlock);

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextColored(ImGuiColors.ParsedOrange, "Not Received:");
        DrawMissingTable(Data.GachaFourZero.Content.Where(i => dict[i] == 0), showUnlock);
    }

    private void Sanctuary()
    {
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
        var unsortedList = Utils.ToSortedEntry(dict, opened);

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Found: {dict.Count(pair => pair.Value > 0)} out of {Data.Sanctuary.Content.Count} items");
        DrawTable(unsortedList, false);

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextColored(ImGuiColors.ParsedOrange, "Not Received:");
        DrawMissingTable(Data.Sanctuary.Content.Where(i => dict[i] == 0), false);
    }

    private void DrawTable(IEnumerable<Utils.SortedEntry> content, bool showUnlocked)
    {
        new SimpleTable<Utils.SortedEntry>("##GachaTable", Utils.SortEntries, ImGuiTableFlags.Sortable)
            .EnableSortSpec()
            .AddIconColumn("##icon", entry => Helper.DrawIcon(entry.Icon))
            .AddColumn("Item##item", entry => Helper.HoverableText(entry.Name))
            .AddColumn("Num##amount", entry => ImGui.TextUnformatted($"x{entry.Obtained}"), initWidth: 0.2f)
            .AddColumn("Pct##percentage", entry => ImGui.TextUnformatted($"{entry.Percentage:F2}%"), ImGuiTableColumnFlags.DefaultSort, 0.25f)
            .AddColumn("##unlocked", entry => Helper.DrawUnlockedSymbol(Unlocked.TryGetValue(entry.Id, out var unlocked) && unlocked), ImGuiTableColumnFlags.NoSort, 0.1f, showUnlocked)
            .Draw(content);
    }

    private void DrawMissingTable(IEnumerable<uint> content, bool showUnlocked)
    {
        new SimpleTable<uint>("##GachaMissingTable", Helper.NoSort)
            .AddIconColumn("##missingItemIcon", entry => Helper.DrawIcon(Utils.CheckItemAction(Sheets.GetItem(entry))))
            .AddColumn("Item##missingItem", entry => Helper.HoverableText(Sheets.GetItem(entry).Name.ToString()))
            .AddColumn("##missingUnlocked", entry => Helper.DrawUnlockedSymbol(Unlocked.TryGetValue(entry, out var unlocked) && unlocked), ImGuiTableColumnFlags.NoSort, 0.1f, showUnlocked)
            .Draw(content);
    }

    private void RefreshUnlocks()
    {
        if (!Utils.NeedsRefresh(ref LastRefresh, RefreshRate))
            return;

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
        return action.Action.RowId switch
        {
            1322 => instance->PlayerState.IsMountUnlocked(action.Data[0]),
            853 => instance->IsCompanionUnlocked(action.Data[0]),
            _ => false
        };
    }
}
