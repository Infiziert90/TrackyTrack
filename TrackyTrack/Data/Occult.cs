using System.Diagnostics.CodeAnalysis;
using Dalamud.Utility;
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
        },
        { OccultTerritory.NorthHorn, new()
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
        },
        { OccultTerritory.NorthHorn, new()
            {
                { OccultTreasureRarity.Bronze, [] },
                { OccultTreasureRarity.Silver, [] },
            }
        },
    };
}

public record OccultResult
{
    public List<OccultItem> Items = [];
}

public record OccultItem(uint Item, uint Count) {}

public struct OccultPot(uint territory, uint rarity, Vector3 pos, uint fateId)
{
    public OccultTerritory Territory = (OccultTerritory)territory;
    public OccultCofferRarity Rarity = (OccultCofferRarity)rarity;
    public Vector3 Position = pos;
    public uint FateId = fateId;

    public List<ItemResult> Received = [];

    [JsonIgnore]
    public bool AwaitingResults;

    public void AddItem(uint item, uint count)
        => Received.Add(new ItemResult(ItemUtil.GetBaseId(item).ItemId, count));

    public bool IsValid => Received.Count > 0;

    public OccultPot Clone()
        => new() {Territory = Territory, Rarity = Rarity, Received = [..Received], Position = Position, FateId = FateId};
}

public struct OccultCoffer(uint territory, uint baseId, Vector3 pos)
{
    public OccultTerritory Territory = (OccultTerritory)territory;
    public uint Base = baseId;
    public OccultTreasureRarity Rarity = (OccultTreasureRarity)Sheets.TreasureSheet.GetRow(baseId).SGB.RowId;
    public Vector3 Position = pos;

    public List<ItemResult> Received = [];

    [JsonIgnore]
    public bool AwaitingResults;

    public void AddItem(uint item, uint count)
        => Received.Add(new ItemResult(ItemUtil.GetBaseId(item).ItemId, count));

    public bool IsValid
    {
        get
        {
            if (Received.Count == 0)
                return false;

            switch (Rarity)
            {
                case OccultTreasureRarity.Bronze when Received.Count > 1:
                case OccultTreasureRarity.Silver when Received.Count > 3:
                    return false;
                default:
                    return true;
            }
        }
    }

    public OccultCoffer Clone()
        => new() {Territory = Territory, Base = Base, Rarity = Rarity, Received = [..Received], Position = Position};
}

public enum OccultTerritory : uint
{
    SouthHorn = 1252,
    NorthHorn = 1346,
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

public enum CombinedRarity : uint
{
    TreasureBronze = 1596,
    TreasureSilver = 1597,

    PotGold = 2014741,
    PotSilver = 2014742,
    PotBronze = 2014743,

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
    public static readonly HashSet<uint> RarityArray = Enum.GetValues<OccultCofferRarity>().Select(x => (uint)x).ToHashSet();

    public static string ToName(this OccultTerritory territory)
    {
        return territory switch
        {
            OccultTerritory.SouthHorn => "South Horn",
            OccultTerritory.NorthHorn => "North Horn",
            _ => "Unknown",
        };
    }

    public static string ToName(this OccultTreasureRarity rarity)
    {
        return rarity switch
        {
            OccultTreasureRarity.Bronze => "Bronze",
            OccultTreasureRarity.Silver => "Silver",
            _ => "Unknown",
        };
    }

    public static string ToExtendedName(this OccultTreasureRarity rarity)
    {
        return rarity switch
        {
            OccultTreasureRarity.Bronze => "Treasure Bronze",
            OccultTreasureRarity.Silver => "Treasure Silver",
            _ => "Unknown",
        };
    }

    public static string ToName(this OccultCofferRarity rarity)
    {
        return rarity switch
        {
            OccultCofferRarity.Bronze => "Bronze",
            OccultCofferRarity.Silver => "Silver",
            OccultCofferRarity.Gold or OccultCofferRarity.BunnyGold => "Gold",
            _ => "Unknown",
        };
    }

    public static string ToExtendedName(this OccultCofferRarity rarity)
    {
        return rarity switch
        {
            OccultCofferRarity.Bronze => "Pot Bronze",
            OccultCofferRarity.Silver => "Pot Silver",
            OccultCofferRarity.Gold => "Pot Gold",
            OccultCofferRarity.BunnyGold => "Bunny Gold",
            _ => "Unknown",
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
            _ => 0,
        };
    }

    public static OccultTreasureRarity ToTreasure(this CombinedRarity rarity)
    {
        return rarity switch
        {
            CombinedRarity.TreasureBronze => OccultTreasureRarity.Bronze,
            CombinedRarity.TreasureSilver => OccultTreasureRarity.Silver,
            _ => OccultTreasureRarity.Bronze,
        };
    }

    public static OccultCofferRarity ToPot(this CombinedRarity rarity)
    {
        return rarity switch
        {
            CombinedRarity.PotBronze => OccultCofferRarity.Bronze,
            CombinedRarity.PotSilver => OccultCofferRarity.Silver,
            CombinedRarity.PotGold => OccultCofferRarity.Gold,
            _ => OccultCofferRarity.Bronze,
        };
    }

    public static OccultCofferRarity ToBunny(this CombinedRarity _)
    {
        return OccultCofferRarity.BunnyGold;
    }

    public static string ToExtendedName(this CombinedRarity rarity)
    {
        return rarity switch
        {
            CombinedRarity.TreasureBronze or CombinedRarity.TreasureSilver => rarity.ToTreasure().ToExtendedName(),
            CombinedRarity.PotBronze or CombinedRarity.PotSilver or CombinedRarity.PotGold => rarity.ToPot().ToExtendedName(),
            CombinedRarity.BunnyGold => rarity.ToBunny().ToExtendedName(),
            _ => "Unknown",
        };
    }
}
