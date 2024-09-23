using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private int SelectedType;
    private static readonly string[] Territories = { "Eureka", "Bozja", "Unknown" };

    private void LockboxTab()
    {
        using var tabItem = ImRaii.TabItem("Lockbox");
        if (!tabItem.Success)
            return;

        using var tabBar = ImRaii.TabBar("##LockboxTabBar");
        if (!tabBar.Success)
            return;

        var characters = Plugin.CharacterStorage.Values;
        if (characters.Count == 0)
        {
            Helper.NoEurekaCofferData();
            return;
        }

        var characterLockboxes = characters.Where(c => c.Lockbox.Opened > 0).ToArray();
        if (characterLockboxes.Length == 0)
        {
            Helper.NoEurekaCofferData();
            return;
        }

        LockboxStats(characterLockboxes);

        foreach (var type in LockboxExtensions.AsArray)
        {
            using var innerTabItem = ImRaii.TabItem(type.ToArea());
            if (innerTabItem.Success)
                LockboxHistory(type, characterLockboxes);
        }
    }

    private void LockboxStats(CharacterConfiguration[] characters)
    {
        using var tabItem = ImRaii.TabItem("Stats");
        if (!tabItem.Success)
            return;

        var longest = 0;
        var totalNumber = 0u;
        var openedTypes = new Dictionary<string, Dictionary<LockboxTypes, uint>>
        {
            { "Eureka", new Dictionary<LockboxTypes, uint>() },
            { "Bozja", new Dictionary<LockboxTypes, uint>() },
            { "Unknown", new Dictionary<LockboxTypes, uint>() },
        };

        foreach (var (type, dict) in characters.SelectMany(c => c.Lockbox.History))
        {
            foreach (var amount in dict.Values)
            {
                totalNumber += amount;
                if (!openedTypes[type.ToTerritory()].TryAdd(type, amount))
                    openedTypes[type.ToTerritory()][type] += amount;

                if (type.ToName().Length > longest)
                    longest = type.ToName().Length;
            }
        }

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.TextColored(ImGuiColors.DalamudViolet, "General:");
        using var table = ImRaii.Table("##TotalStatsTable", 2);

        ImGui.TableSetupColumn("##stat", 0, 2.0f);
        ImGui.TableSetupColumn("##opened");

        using var indent = ImRaii.PushIndent(10.0f);
        ImGui.TableNextColumn();
        ImGui.TextColored(ImGuiColors.HealerGreen, "Opened");

        ImGui.TableNextColumn();
        Helper.RightTextColored(ImGuiColors.HealerGreen, $"{totalNumber:N0} Treasure{(totalNumber > 1 ? "s" : "")}");

        foreach (var territory in Territories)
        {
            if (openedTypes[territory].Count == 0)
                continue;

            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, territory);

            ImGui.TableNextColumn();
            var opened = openedTypes[territory].Values.Sum(a => a);
            var containerName = LockboxExtensions.TerritoryToContainerName(territory);
            Helper.RightTextColored(ImGuiColors.HealerGreen, $"{opened:N0} {containerName}");

            using var innerIndent = ImRaii.PushIndent(10.0f);
            foreach (var (type, amount) in openedTypes[territory].OrderBy(pair => pair.Key))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(type.ToName());

                ImGui.TableNextColumn();
                Helper.RightAlignedText($"x{amount:N0}", 10.0f);
            }
        }
    }

    private void LockboxHistory(LockboxTypes types, CharacterConfiguration[] characters)
    {
        var bothTypes = types.ToMultiple();
        if (types.HasMultiple())
        {
            if (SelectedType != (int) bothTypes.Main && SelectedType != (int) bothTypes.Secondary)
                SelectedType = (int) bothTypes.Main;

            ImGui.RadioButton(bothTypes.Main.ToName(), ref SelectedType, (int) bothTypes.Main);
            ImGui.SameLine();
            ImGui.RadioButton(bothTypes.Secondary.ToName(), ref SelectedType, (int) bothTypes.Secondary);
        }
        else
        {
            SelectedType = (int) types;
        }

        // fill dict with real values
        var selectedType = (LockboxTypes) SelectedType;
        var dict = new Dictionary<uint, uint>();
        foreach (var (_, lockboxDict) in characters.SelectMany(c => c.Lockbox.History).Where(pair => pair.Key == selectedType))
        {
            foreach (var (itemId, amount) in lockboxDict)
            {
                if (!dict.TryAdd(itemId, amount))
                    dict[itemId] += amount;
            }
        }

        ImGuiHelpers.ScaledDummy(5.0f);
        if (dict.Count == 0)
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Haven't opened any {selectedType.ToName()} Lockboxes.");
            return;
        }

        var opened = dict.Values.Sum(s => s);
        var unsortedList = dict.Select(pair =>
        {
            var item = ItemSheet.GetRow(pair.Key)!;
            var count = pair.Value;
            var percentage = (double) pair.Value / opened * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), count, percentage);
        });

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        new SimpleTable<Utils.SortedEntry>("##HistoryTable", Utils.SortEntries, ImGuiTableFlags.Sortable, withIndent: 10.0f)
            .EnableSortSpec()
            .AddColumn("##icon", entry => Helper.DrawIcon(entry.Icon), ImGuiTableColumnFlags.NoSort, 0.17f)
            .AddColumn("Item##item", entry => Helper.HoverableText(entry.Name))
            .AddColumn("Num##amount", entry => ImGui.TextUnformatted($"x{entry.Count}"), initWidth: 0.2f)
            .AddColumn("Pct##percentage", entry => ImGui.TextUnformatted($"{entry.Percentage:F2}%"), ImGuiTableColumnFlags.DefaultSort, 0.25f)
            .Draw(unsortedList);
    }
}
