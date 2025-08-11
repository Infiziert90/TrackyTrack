using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private uint SelectedType;
    private readonly Dictionary<uint, Dictionary<uint, uint>> LockboxContent = [];

    private static readonly string[] Territories = ["Eureka", "Bozja", "Unknown"];

    private long LastLockboxRefresh;
    private const int LockboxRefreshRate = 30_000; // 30s

    private void LockboxTab()
    {
        using var tabItem = ImRaii.TabItem("Lockbox");
        if (!tabItem.Success)
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

        RefreshLockbox(characterLockboxes);

        var styles = ImGui.GetStyle();
        var nameDict = new SortedDictionary<uint, (string Name, float Width)>();
        foreach (var lockboxId in LockboxContent.Keys)
        {
            var name = Sheets.GetItem(lockboxId).Name.ExtractText();
            nameDict[lockboxId] = (name, ImGui.CalcTextSize(name).X + (styles.ItemSpacing.X * 2));
        }

        var pos = ImGui.GetCursorPos();

        var childSize = new Vector2(nameDict.Select(pair => pair.Value.Width).Max(), 0);
        using (var tabChild = ImRaii.Child("Tabs", childSize, true))
        {
            if (tabChild.Success)
            {
                if (ImGui.Selectable("Stats", SelectedType == 0))
                    SelectedType = 0;

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                foreach (var (lockboxId, (name, _)) in nameDict)
                    if (ImGui.Selectable(name, SelectedType == lockboxId))
                        SelectedType = lockboxId;
            }
        }

        ImGui.SetCursorPos(pos with {X = pos.X + childSize.X});
        using (var contentChild = ImRaii.Child("Content", Vector2.Zero, true))
        {
            if (contentChild.Success)
            {
                if (SelectedType == 0)
                    LockboxStats(characterLockboxes);
                else
                    LockboxHistory();
            }
        }
    }

    private void LockboxStats(CharacterConfiguration[] characters)
    {
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
        using var table = ImRaii.Table("##TotalStatsTable", 2);
        ImGui.TableSetupColumn("##stat", 0, 2.0f);
        ImGui.TableSetupColumn("##opened");

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

    private void LockboxHistory()
    {
        var content = LockboxContent[SelectedType];

        var opened = content.Values.Sum(s => s);
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        if (content.Count == 0)
            return;

        var unsortedList = Utils.ToSortedEntry(content, (int)opened);
        new SimpleTable<Utils.SortedEntry>("##HistoryTable", Utils.SortEntries, ImGuiTableFlags.Sortable)
            .EnableSortSpec()
            .AddIconColumn("##icon", entry => Helper.DrawIcon(entry.Icon))
            .AddColumn("Item##item", entry => Helper.HoverableText(entry.Name))
            .AddColumn("Num##amount", entry => ImGui.TextUnformatted($"x{entry.Obtained}"), initWidth: 0.2f)
            .AddColumn("Pct##percentage", entry => ImGui.TextUnformatted($"{entry.Percentage:F2}%"), ImGuiTableColumnFlags.DefaultSort, 0.25f)
            .Draw(unsortedList);
    }

    private void RefreshLockbox(CharacterConfiguration[] characters)
    {
        if (!Utils.NeedsRefresh(ref LastLockboxRefresh, LockboxRefreshRate))
            return;

        LockboxContent.Clear();
        foreach (var (type, innerDict) in characters.SelectMany(s => s.Lockbox.History))
        {
            if (Lockboxes.Logograms.Contains((uint)type) || Lockboxes.Fragments.Contains((uint)type))
                continue;

            LockboxContent.TryAdd((uint)type, []);

            var lockbox = LockboxContent[(uint)type];
            foreach (var (itemId, quantity) in innerDict.ToArray())
                if (!lockbox.TryAdd(itemId, quantity))
                    lockbox[itemId] += quantity;
        }
    }
}
