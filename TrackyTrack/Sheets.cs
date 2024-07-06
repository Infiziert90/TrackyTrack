using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace TrackyTrack;

public static class Sheets
{
    public static readonly ExcelSheet<GatheringPoint> GatheringPoints;

    static Sheets()
    {
        GatheringPoints = Plugin.Data.GetExcelSheet<GatheringPoint>()!;
    }
}
