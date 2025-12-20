using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace TrackyTrack.Data;

[SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
public class OccultTracker
{
    public uint Opened = 0;
    public readonly Dictionary<OccultTerritory, Dictionary<OccultCofferRarity, Dictionary<DateTime, OccultResult>>> History = new()
    {
        { OccultTerritory.SouthHorn, new()
            {
                { OccultCofferRarity.Bronze, [] },
                { OccultCofferRarity.Silver, [] },
                { OccultCofferRarity.Gold, [] },
                { OccultCofferRarity.BunnyGold, [] },
            }
        }
    };

    public uint TreasureOpened = 0;
    public readonly Dictionary<OccultTerritory, Dictionary<OccultTreasureRarity, Dictionary<DateTime, OccultResult>>> TreasureHistory = new()
    {
        { OccultTerritory.SouthHorn, new()
            {
                { OccultTreasureRarity.Bronze, [] },
                { OccultTreasureRarity.Silver, [] },
            }
        }
    };
}

public record OccultResult
{
    public readonly List<OccultItem> Items = [];

    public void AddItem(uint item, uint count) => Items.Add(new OccultItem(item, count));
    [JsonIgnore] public bool IsValid => Items.Count != 0;
}

public record OccultItem(uint Item, uint Count)
{
    public uint[] Combine() => [Item, Count];
}

public enum OccultTerritory : uint
{
    SouthHorn = 1252,
}

public enum OccultTreasureRarity : uint
{
    Bronze = 1596,
    Silver = 1597,
}

public enum OccultCofferRarity : uint
{
    Gold = 2014741,
    Silver = 2014742,
    Bronze = 2014743,

    BunnyGold = 2012936,
}

public enum OccultWorth
{
    Bronze = 1_000,
    Silver = 5_000,
    Gold = 30_000,
    BunnyGold = 200_000
}

public static class OccultUtil
{
    public static (long Total, Dictionary<OccultTerritory, Dictionary<OccultTreasureRarity, int>> Dict) GetTreasureAmounts(IEnumerable<CharacterConfiguration> characters)
    {
        var totalNumber = 0;
        var territoryCoffers = new Dictionary<OccultTerritory, Dictionary<OccultTreasureRarity, int>>();
        foreach (var (territory, rarityDictionary) in characters.SelectMany(c => c.Occult.TreasureHistory))
        {
            if (!territoryCoffers.ContainsKey(territory))
                territoryCoffers[territory] = [];

            foreach (var (rarity, history) in rarityDictionary)
            {
                totalNumber += history.Count;

                if (!territoryCoffers[territory].TryAdd(rarity, history.Count))
                    territoryCoffers[territory][rarity] += history.Count;
            }
        }

        return (totalNumber, territoryCoffers);
    }

    public static (long Worth, long Total, Dictionary<OccultTerritory, Dictionary<OccultCofferRarity, int>> Dict) GetPotAmounts(IEnumerable<CharacterConfiguration> characters)
    {
        var worth = 0L;
        var totalNumber = 0;
        var territoryCoffers = new Dictionary<OccultTerritory, Dictionary<OccultCofferRarity, int>>();
        foreach (var (territory, rarityDictionary) in characters.SelectMany(c => c.Occult.History))
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

public static class OccultExtensions
{
    public static readonly uint[] RarityArray = Enum.GetValues<OccultCofferRarity>().Select(x => (uint)x).ToArray();
    public static readonly int[] WorthArray =  Enum.GetValues<OccultWorth>().Select(x => (int)x).ToArray();

    public static string ToName(this OccultTerritory territory)
    {
        return territory switch
        {
            OccultTerritory.SouthHorn => "South Horn",
            _ => "Unknown"
        };
    }

    public static string ToName(this OccultTreasureRarity rarity)
    {
        return rarity switch
        {
            OccultTreasureRarity.Bronze => "Bronze",
            OccultTreasureRarity.Silver => "Silver",
            _ => "Unknown"
        };
    }

    public static string ToName(this OccultCofferRarity rarity)
    {
        return rarity switch
        {
            OccultCofferRarity.Bronze => "Bronze",
            OccultCofferRarity.Silver => "Silver",
            OccultCofferRarity.Gold or OccultCofferRarity.BunnyGold => "Gold",
            _ => "Unknown"
        };
    }

    public static uint ToWorth(this OccultCofferRarity rarity)
    {
        return rarity switch
        {
            OccultCofferRarity.BunnyGold => (uint)OccultWorth.BunnyGold,
            OccultCofferRarity.Gold => (uint)OccultWorth.Gold,
            OccultCofferRarity.Silver => (uint)OccultWorth.Silver,
            OccultCofferRarity.Bronze => (uint)OccultWorth.Bronze,
            _ => 0
        };
    }

    public static OccultCofferRarity FromWorth(long worth)
    {
        return (OccultWorth)worth switch
        {
            OccultWorth.BunnyGold => OccultCofferRarity.BunnyGold,
            OccultWorth.Gold => OccultCofferRarity.Gold,
            OccultWorth.Silver => OccultCofferRarity.Silver,
            OccultWorth.Bronze => OccultCofferRarity.Bronze,
            _ => 0
        };
    }
}