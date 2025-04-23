using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace TrackyTrack;

public static class Sheets
{
    public static readonly ExcelSheet<Map> MapSheet;
    public static readonly ExcelSheet<Item> ItemSheet;
    public static readonly ExcelSheet<Treasure> TreasureSheet;
    public static readonly ExcelSheet<ParamGrow> ParamGrowSheets;
    public static readonly ExcelSheet<GCSupplyDutyReward> GCSupplySheet;
    public static readonly ExcelSheet<TerritoryType> TerritoryTypeSheet;
    public static readonly ExcelSheet<TerritoryTypeTransient> TerritoryTransientSheet;

    public static readonly uint MaxLevel;

    static Sheets()
    {
        MapSheet = Plugin.Data.GetExcelSheet<Map>();
        ItemSheet = Plugin.Data.GetExcelSheet<Item>();
        TreasureSheet = Plugin.Data.GetExcelSheet<Treasure>();
        ParamGrowSheets = Plugin.Data.GetExcelSheet<ParamGrow>();
        GCSupplySheet = Plugin.Data.GetExcelSheet<GCSupplyDutyReward>();
        TerritoryTypeSheet = Plugin.Data.GetExcelSheet<TerritoryType>();
        TerritoryTransientSheet = Plugin.Data.GetExcelSheet<TerritoryTypeTransient>();

        MaxLevel = ParamGrowSheets.Where(l => l.ExpToNext > 0).Max(l => l.RowId);
    }

    public static Item GetItem(uint itemId) => ItemSheet.GetRow(Utils.NormalizeItemId(itemId));
}
