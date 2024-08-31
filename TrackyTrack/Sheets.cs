using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace TrackyTrack;

public static class Sheets
{
    public static readonly ExcelSheet<Item> ItemSheet;
    public static readonly ExcelSheet<GatheringPoint> GatheringPoints;

    static Sheets()
    {
        ItemSheet = Plugin.Data.GetExcelSheet<Item>()!;
        GatheringPoints = Plugin.Data.GetExcelSheet<GatheringPoint>()!;
    }
}
