using System.Diagnostics.CodeAnalysis;
using Dalamud.Utility;
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
                { CofferRarity.Bronze, [] },
                { CofferRarity.Silver, [] },
                { CofferRarity.Gold, [] },
            }
        },
        { Territory.Pyros, new()
            {
                { CofferRarity.Bronze, [] },
                { CofferRarity.Silver, [] },
                { CofferRarity.Gold, [] },
            }
        },
        { Territory.Hydatos, new()
            {
                { CofferRarity.Bronze, [] },
                { CofferRarity.Silver, [] },
                { CofferRarity.Gold, [] },
            }
        }
    };
}

public record EurekaResult
{
    public readonly List<EurekaItem> Items = [];

    public void AddItem(uint item, uint count) => Items.Add(new EurekaItem(ItemUtil.GetBaseId(item).ItemId, count));
    [JsonIgnore] public bool IsValid => Items.Count != 0;
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

public enum Worth
{
    Bronze = 10_000,
    Silver = 25_000,
    Gold = 100_000,
}

public static class EurekaUtil
{
    public static (long Worth, long Total, Dictionary<Territory, Dictionary<CofferRarity, int>> Dict) GetAmounts(IEnumerable<CharacterConfiguration> characters)
    {
        var worth = 0L;
        var totalNumber = 0;
        var territoryCoffers = new Dictionary<Territory, Dictionary<CofferRarity, int>>();
        foreach (var (territory, rarityDictionary) in characters.SelectMany(c => c.Eureka.History))
        {
            if (!territoryCoffers.ContainsKey(territory))
                territoryCoffers[territory] = [];

            foreach (var (rarity, history) in rarityDictionary)
            {
                totalNumber += history.Count;
                worth += history.Count * rarity.ToWorth();

                if (!territoryCoffers[territory].TryAdd(rarity, history.Count))
                    territoryCoffers[territory][rarity] += history.Count;
            }
        }

        return (worth, totalNumber, territoryCoffers);
    }
}

public static class EurekaExtensions
{
    public static readonly uint[] RarityArray = Enum.GetValues<Territory>().Select(t => (uint) t).ToArray();
    public static readonly int[] WorthArray =  Enum.GetValues<Worth>().Select(x => (int)x).ToArray();

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
        return rarity switch
        {
            CofferRarity.Gold => (uint)Worth.Gold,
            CofferRarity.Silver => (uint)Worth.Silver,
            CofferRarity.Bronze => (uint)Worth.Bronze,
            _ => 0
        };
    }

    public static CofferRarity FromWorth(long worth)
    {
        return (Worth)worth switch
        {
            Worth.Gold => CofferRarity.Gold,
            Worth.Silver => CofferRarity.Silver,
            Worth.Bronze => CofferRarity.Bronze,
            _ => 0
        };
    }
}
