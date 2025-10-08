using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace TrackyTrack;

public static class Sheets
{
    public static readonly ExcelSheet<Item> ItemSheet;
    public static readonly ExcelSheet<Mount> MountSheet;
    public static readonly ExcelSheet<ParamGrow> ParamGrowSheets;
    public static readonly ExcelSheet<GCSupplyDutyReward> GCSupplySheet;

    public static readonly uint MaxLevel;

    public static readonly Item[] DesynthCache;
    public static readonly int HighestILvl;

    public static readonly uint LowewstValidId;
    public static readonly uint HighestValidId;

    static Sheets()
    {
        ItemSheet = Plugin.Data.GetExcelSheet<Item>();
        MountSheet = Plugin.Data.GetExcelSheet<Mount>();
        ParamGrowSheets = Plugin.Data.GetExcelSheet<ParamGrow>();
        GCSupplySheet = Plugin.Data.GetExcelSheet<GCSupplyDutyReward>();

        MaxLevel = ParamGrowSheets.Where(l => l.ExpToNext > 0).Max(l => l.RowId);

        DesynthCache = ItemSheet.Where(i => i.Desynth > 0).ToArray();
        HighestILvl = DesynthCache.Select(i => (int)i.LevelItem.RowId).Max();

        LowewstValidId = 100;
        HighestValidId = ItemSheet.Where(i => i.Icon > 0).MaxBy(i => i.RowId).RowId;
    }

    public static Item GetItem(uint itemId) => ItemSheet.GetRow(ItemUtil.GetBaseId(itemId).ItemId);
}
