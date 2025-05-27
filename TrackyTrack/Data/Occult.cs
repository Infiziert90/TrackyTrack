using Newtonsoft.Json;

namespace TrackyTrack.Data;

public record OccultResult
{
    public readonly List<OccultItem> Items = [];

    public void AddItem(uint item, uint count) => Items.Add(new OccultItem(Utils.NormalizeItemId(item), count));
    [JsonIgnore] public bool IsValid => Items.Count != 0;
}

public record OccultItem(uint Item, uint Count);

public enum OccultTerritory : uint
{
    SouthHorn = 1252,
}

public enum OccultCofferRarity : uint
{
    Gold = 2014741,
    Silver = 2014742,
    Bronze = 2014743
}

public static class OccultExtensions
{
    public static readonly uint[] AsArray = Enum.GetValues<OccultCofferRarity>().Select(x => (uint)x).ToArray();

    public static string ToName(this OccultTerritory territory)
    {
        return territory switch
        {
            OccultTerritory.SouthHorn => "South Horn",
            _ => "Unknown"
        };
    }

    public static string ToName(this OccultCofferRarity rarity)
    {
        return rarity switch
        {
            OccultCofferRarity.Bronze => "Bronze",
            OccultCofferRarity.Silver => "Silver",
            OccultCofferRarity.Gold => "Gold",
            _ => "Unknown"
        };
    }

    public static uint ToWorth(this OccultCofferRarity rarity)
    {
        return rarity switch
        {
            OccultCofferRarity.Gold => 30_000,
            OccultCofferRarity.Silver => 5_000,
            OccultCofferRarity.Bronze => 1_000,
            _ => 0
        };
    }

    public static OccultCofferRarity FromWorth(long worth)
    {
        return worth switch
        {
            30_000 => OccultCofferRarity.Gold,
            5_000 => OccultCofferRarity.Silver,
            1_000 => OccultCofferRarity.Bronze,
            _ => 0
        };
    }
}