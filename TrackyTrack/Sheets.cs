using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace TrackyTrack;

public static class Sheets
{
    public static readonly ExcelSheet<Map> MapSheet;
    public static readonly ExcelSheet<Item> ItemSheet;
    public static readonly ExcelSheet<Treasure> TreasureSheet;
    public static readonly ExcelSheet<GCSupplyDutyReward> GCSupplySheet;
    public static readonly ExcelSheet<TerritoryTypeTransient> TerritoryTransientSheet;

    static Sheets()
    {
        MapSheet = Plugin.Data.GetExcelSheet<Map>();
        ItemSheet = Plugin.Data.GetExcelSheet<Item>();
        TreasureSheet = Plugin.Data.GetExcelSheet<Treasure>();
        GCSupplySheet = Plugin.Data.GetExcelSheet<GCSupplyDutyReward>();
        TerritoryTransientSheet = Plugin.Data.GetExcelSheet<TerritoryTypeTransient>();
    }

    public static Item GetItem(uint itemId) => ItemSheet.GetRow(Utils.NormalizeItemId(itemId));
}
