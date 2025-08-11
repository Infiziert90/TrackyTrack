using Dalamud.Interface.Utility.Raii;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    public int SelectedLootIndex;

    private void LootTab()
    {
        using var tabItem = ImRaii.TabItem("Loot##LootStats");
        if (!tabItem.Success)
            return;

        Loot();
    }

    private void Loot()
    {
        using var child = ImRaii.Child("ContentChild", Vector2.Zero, true);
        if (!child.Success)
            return;

        if (Plugin.Importer.DutyLootCache.Count == 0)
            return;

        var lootDutyList = Plugin.Importer.DutyLootCache.Values.Select(v => v.DutyName).ToArray();
        ImGui.Combo("Duty List", ref SelectedLootIndex, lootDutyList, lootDutyList.Length);

        var duty = Plugin.Importer.DutyLootCache.Values.First(v => v.DutyName == lootDutyList[SelectedLootIndex]);
        foreach (var (key, chest) in duty.Chests)
        {
            var map = Sheets.MapSheet.GetRow(chest.MapId);
            Helper.WrappedError($"{map.PlaceNameSub.Value.Name.ExtractText()} ({chest.ChestId} | {chest.Position.X:F2}/{chest.Position.Y:F2}/{chest.Position.Z:F2}) [Records: {chest.Records} | Unique Items: {chest.Rewards.Count}]:");

            var unsortedList = chest.Rewards.OrderBy(pair => pair.Key).Select(pair =>
            {
                var item = Sheets.GetItem(pair.Key);
                var count = pair.Value.Obtained;
                var percentage = count / (double) chest.Records * 100.0;
                return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), count, 0, 0, percentage);
            });

            new SimpleTable<Utils.SortedEntry>($"##LootTable{key}", Utils.SortEntries, ImGuiTableFlags.Sortable)
                .EnableSortSpec()
                .AddIconColumn("##icon", entry => Helper.DrawIcon(entry.Icon))
                .AddColumn("Item##item", entry => Helper.HoverableText(entry.Name))
                .AddColumn("Num##amount", entry => ImGui.TextUnformatted($"x{entry.Obtained}"), initWidth: 0.2f)
                .AddColumn("Pct##percentage", entry => ImGui.TextUnformatted($"{entry.Percentage:F2}%"), ImGuiTableColumnFlags.DefaultSort, 0.25f)
                .Draw(unsortedList);
        }
    }
}
