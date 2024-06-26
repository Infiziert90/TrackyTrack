﻿using System.Diagnostics.CodeAnalysis;
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

    // Bozja Fragments
    public static readonly uint[] Fragments =
    {
        30884, 30885, 30886, 30887, 30888, 30889, 30890, 30891,
        30892, 30893, 30894, 30895, 30896, 30897, 30898, 30899,
        32162, 32163, 32164, 32165, 32831, 32832, 32833, 32834,
        33768, 33769, 33770, 33771, 33772, 33773, 33774, 33775,
        33776, 33777, 33778, 33779
    };

    // Triple Triad Card Packs
    public static readonly uint[] CardPacks =
    {
        10077, 10128, 10129, 10130, 13380, 17690, 17691, 17692,
        17693, 17694, 17695, 17696, 17697, 17698, 17699, 17700,
        17701, 17702, 28652
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
        return type switch
        {
            LockboxTypes.Anemos => "Anemos",
            LockboxTypes.Pagos => "Pagos",
            LockboxTypes.ColdWarped => "Pagos",
            LockboxTypes.Pyros => "Pyros",
            LockboxTypes.HeatWarped => "Pyros",
            LockboxTypes.Hydatos => "Hydatos",
            LockboxTypes.MoistureWarped => "Hydatos",
            LockboxTypes.SouthernFront => "Bozja",
            LockboxTypes.Zadnor => "Bozja",
            _ => "Unknown"
        };
    }

    public static string ToTerritory(this LockboxTypes type)
    {
        return type switch
        {
            LockboxTypes.Anemos => "Eureka",
            LockboxTypes.Pagos => "Eureka",
            LockboxTypes.ColdWarped => "Eureka",
            LockboxTypes.Pyros => "Eureka",
            LockboxTypes.HeatWarped => "Eureka",
            LockboxTypes.Hydatos => "Eureka",
            LockboxTypes.MoistureWarped => "Eureka",
            LockboxTypes.SouthernFront => "Bozja",
            LockboxTypes.Zadnor => "Bozja",
            _ => "Unknown"
        };
    }

    public static string ToName(this LockboxTypes type)
    {
        return type switch
        {
            LockboxTypes.Anemos => "Anemos",
            LockboxTypes.Pagos => "Pagos",
            LockboxTypes.ColdWarped => "Cold-Warped",
            LockboxTypes.Pyros => "Pyros",
            LockboxTypes.HeatWarped => "Heat-Warped",
            LockboxTypes.Hydatos => "Hydatos",
            LockboxTypes.MoistureWarped => "Moisture-Warped",
            LockboxTypes.SouthernFront => "Bozja",
            LockboxTypes.Zadnor => "Zadnor",
            _ => Utils.ToStr(ItemSheet.GetRow((uint) type)!.Name),
        };
    }

    public static (LockboxTypes Main, LockboxTypes Secondary) ToMultiple(this LockboxTypes type)
    {
        return type switch
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

    public static string TerritoryToContainerName(string territory)
    {
        return territory switch
        {
            "Eureka" => "Boxes",
            "Bozja" => "Boxes",
            _ => "Things"
        };
    }
}

