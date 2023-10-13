using System.Diagnostics.CodeAnalysis;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace TrackyTrack.Data;

[SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
public class Lockboxes
{
    public int Opened = 0;

    public readonly Dictionary<LockboxTypes, Dictionary<uint, uint>> History = new()
    {
        { LockboxTypes.Anemos, new() },
        { LockboxTypes.Pagos, new() },
        { LockboxTypes.ColdWarped, new() },
        { LockboxTypes.Pyros, new() },
        { LockboxTypes.HeatWarped, new() },
        { LockboxTypes.Hydatos, new() },
        { LockboxTypes.MoistureWarped, new() },

        { LockboxTypes.SouthernFront, new() },
        { LockboxTypes.Zadnor, new() },
    };
}

public enum LockboxTypes : uint
{
    // Eureka
    Anemos = 22508,
    Pagos = 23142,
    ColdWarped = 23379,
    Pyros = 24141,
    HeatWarped = 24142,
    Hydatos = 24848,
    MoistureWarped = 24849,

    // Bozja
    SouthernFront = 31357,
    Zadnor = 33797,
}

public static class LockboxExtensions
{
    private static readonly ExcelSheet<Item> ItemSheet;
    static LockboxExtensions()
    {
        ItemSheet = Plugin.Data.GetExcelSheet<Item>()!;
    }

    public static readonly LockboxTypes[] AsArray =
    {
        LockboxTypes.Anemos,
        LockboxTypes.Pagos,
        LockboxTypes.Pyros,
        LockboxTypes.Hydatos,
        LockboxTypes.SouthernFront,
    };

    public static string ToArea(this LockboxTypes type)
    {
        return (type) switch
        {
            LockboxTypes.Anemos => "Anemos",
            LockboxTypes.Pagos => "Pagos",
            LockboxTypes.ColdWarped => "Pagos",
            LockboxTypes.Pyros => "Pyros",
            LockboxTypes.HeatWarped => "HeatWarped",
            LockboxTypes.Hydatos => "Hydatos",
            LockboxTypes.MoistureWarped => "MoistureWarped",
            LockboxTypes.SouthernFront => "Bozja",
            LockboxTypes.Zadnor => "Bozja",
            _ => "Unknown"
        };
    }

    public static string ToName(this LockboxTypes type)
    {
        return (type) switch
        {
            LockboxTypes.Anemos => "Anemos",
            LockboxTypes.Pagos => "Pagos",
            LockboxTypes.ColdWarped => "Cold-Warped",
            LockboxTypes.Pyros => "Pyros",
            LockboxTypes.HeatWarped => "Heat-Warped",
            LockboxTypes.Hydatos => "Hydatos",
            LockboxTypes.MoistureWarped => "Moisture-Warped",
            LockboxTypes.SouthernFront => "Southern Front",
            LockboxTypes.Zadnor => "Zadnor",
            _ => Utils.ToStr(ItemSheet.GetRow((uint) type)!.Name),
        };
    }

    public static (LockboxTypes Main, LockboxTypes Secondary) ToMultiple(this LockboxTypes type)
    {
        return (type) switch
        {
            LockboxTypes.Pagos => (LockboxTypes.Pagos, LockboxTypes.ColdWarped),
            LockboxTypes.Pyros => (LockboxTypes.Pyros, LockboxTypes.HeatWarped),
            LockboxTypes.Hydatos => (LockboxTypes.Hydatos, LockboxTypes.MoistureWarped),
            LockboxTypes.SouthernFront => (LockboxTypes.SouthernFront, LockboxTypes.Zadnor),
            _ => (LockboxTypes.Anemos, LockboxTypes.Anemos)
        };
    }

    public static bool HasMultiple(this LockboxTypes type)
    {
        return type != LockboxTypes.Anemos;
    }
}

