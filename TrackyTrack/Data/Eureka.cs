using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace TrackyTrack.Data;

[SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
public class EurekaTracker
{
    public uint Opened = 0;
    public readonly Dictionary<Territory, Dictionary<CofferRarity, Dictionary<DateTime, EurekaResult>>> History = new()
    {
        { Territory.Pagos, new()
            {
                { CofferRarity.Bronze, new() },
                { CofferRarity.Silver, new() },
                { CofferRarity.Gold, new() },
            }
        },
        { Territory.Pyros, new()
            {
                { CofferRarity.Bronze, new() },
                { CofferRarity.Silver, new() },
                { CofferRarity.Gold, new() },
            }
        },
        { Territory.Hydatos, new()
            {
                { CofferRarity.Bronze, new() },
                { CofferRarity.Silver, new() },
                { CofferRarity.Gold, new() },
            }
        }
    };
}

public record EurekaResult
{
    public readonly List<EurekaItem> Items = new();

    public void AddItem(uint item, uint count) => Items.Add(new EurekaItem(item, count));
    [JsonIgnore] public bool IsValid => Items.Any();
}

public record EurekaItem(uint Item, uint Count);

public enum Territory : uint
{
    Pagos = 763,
    Pyros = 795,
    Hydatos = 827
}

public enum CofferRarity : uint
{
    Gold = 2009530,
    Silver = 2009531,
    Bronze = 2009532
}

public static class EurekaExtensions
{
    public static readonly uint[] AsArray = (uint[]) Enum.GetValues(typeof(Territory));

    public static string ToName(this Territory territory)
    {
        return territory switch
        {
            Territory.Pagos => "Pagos",
            Territory.Pyros => "Pyros",
            Territory.Hydatos => "Hydatos",
            _ => "Unknown"
        };
    }

    public static string ToName(this CofferRarity rarity)
    {
        return rarity switch
        {
            CofferRarity.Bronze => "Bronze",
            CofferRarity.Silver => "Silver",
            CofferRarity.Gold => "Gold",
            _ => "Unknown"
        };
    }

    public static uint ToWorth(this CofferRarity rarity)
    {
        return (rarity) switch
        {
            CofferRarity.Gold => 100_000,
            CofferRarity.Silver => 25_000,
            CofferRarity.Bronze => 10_000,
            _ => 0
        };
    }

    public static CofferRarity FromWorth(long worth)
    {
        return worth switch
        {
            100_000 => CofferRarity.Gold,
            25_000 => CofferRarity.Silver,
            10_000 => CofferRarity.Bronze,
            _ => 0
        };
    }
}
