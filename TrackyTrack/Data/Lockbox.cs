using System.Diagnostics.CodeAnalysis;

namespace TrackyTrack.Data;

[SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
public class Lockboxes
{
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

