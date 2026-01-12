using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace TrackyTrack;

public static class Sheets
{
    public static readonly ExcelSheet<Item> ItemSheet;
    public static readonly ExcelSheet<Mount> MountSheet;
    public static readonly ExcelSheet<Treasure> TreasureSheet;
    public static readonly ExcelSheet<ParamGrow> ParamGrowSheets;
    public static readonly ExcelSheet<GCSupplyDutyReward> GCSupplySheet;
    public static readonly ExcelSheet<TerritoryType> TerritoryTypeSheet;
    public static readonly ExcelSheet<BNpcName> BNPCNameSheet;
    public static readonly ExcelSheet<Pet> PetSheet;
    public static readonly ExcelSheet<Companion> CompanionSheet;

    public static readonly uint MaxLevel;

    public static readonly Item[] DesynthCache;
    public static readonly int HighestILvl;

    public static readonly uint LowewstValidId;
    public static readonly uint HighestValidId;

    public static HashSet<uint> DisallowedBnpcBase = [3705];
    public static HashSet<uint> DisallowedBnpcNames;

    static Sheets()
    {
        ItemSheet = Plugin.Data.GetExcelSheet<Item>();
        MountSheet = Plugin.Data.GetExcelSheet<Mount>();
        TreasureSheet = Plugin.Data.GetExcelSheet<Treasure>();
        ParamGrowSheets = Plugin.Data.GetExcelSheet<ParamGrow>();
        GCSupplySheet = Plugin.Data.GetExcelSheet<GCSupplyDutyReward>();
        TerritoryTypeSheet = Plugin.Data.GetExcelSheet<TerritoryType>();
        BNPCNameSheet = Plugin.Data.GetExcelSheet<BNpcName>();
        PetSheet = Plugin.Data.GetExcelSheet<Pet>();
        CompanionSheet = Plugin.Data.GetExcelSheet<Companion>();

        MaxLevel = ParamGrowSheets.Where(l => l.ExpToNext > 0).Max(l => l.RowId);

        DesynthCache = ItemSheet.Where(i => i.Desynth > 0).ToArray();
        HighestILvl = DesynthCache.Select(i => (int)i.LevelItem.RowId).Max();

        LowewstValidId = 100;
        HighestValidId = ItemSheet.Where(i => i.Icon > 0).MaxBy(i => i.RowId).RowId;

        var pets = PetSheet.Select(c => c.Name.ToString()).Where( c => c.Length > 0 ).ToArray();
        var companions = CompanionSheet.Select(c => c.Singular.ToString()).Where( c => c.Length > 0 ).ToArray();

        DisallowedBnpcNames = BNPCNameSheet.Where(c =>
        {
            var name = c.Singular.ToString();
            if (name.Length == 0)
                return false;

            return pets.Contains(name) || companions.Contains(name);
        }).Select(c => c.RowId).ToHashSet();
    }

    public static Item GetItem(uint itemId) => ItemSheet.GetRow(ItemUtil.GetBaseId(itemId).ItemId);
}
