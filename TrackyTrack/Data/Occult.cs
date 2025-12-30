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

     public static readonly (Vector3, uint)[] TreasurePositions = [
        (new Vector3(-283.98572f, 115.983765f, 377.03516f), 1597), // Counter: 16603
        (new Vector3(697.322f, 69.99304f, 597.9247f), 1597), // Counter: 11748
        (new Vector3(770.7484f, 107.98804f, -143.5722f), 1597), // Counter: 10970
        (new Vector3(277.7904f, 103.77649f, 241.90125f), 1596), // Counter: 10622
        (new Vector3(-401.66327f, 85.03845f, 332.5398f), 1596), // Counter: 10574
        (new Vector3(517.7539f, 67.88733f, 236.1333f), 1597), // Counter: 10430
        (new Vector3(-372.67108f, 74.99805f, 527.4281f), 1596), // Counter: 10027
        (new Vector3(256.1532f, 73.16687f, 492.3628f), 1596), // Counter: 9527
        (new Vector3(-444.11383f, 90.684326f, 26.230225f), 1596), // Counter: 9492
        (new Vector3(-645.68555f, 202.99072f, 710.17017f), 1597), // Counter: 9465
        (new Vector3(-118.97461f, 4.989685f, -708.4612f), 1596), // Counter: 9234
        (new Vector3(-487.11377f, 98.527466f, -205.46277f), 1596), // Counter: 9047
        (new Vector3(609.61304f, 107.98804f, 117.2655f), 1596), // Counter: 8863
        (new Vector3(726.28357f, 108.140625f, -67.91791f), 1596), // Counter: 8862
        (new Vector3(779.0187f, 96.08594f, -256.2448f), 1596), // Counter: 8858
        (new Vector3(-825.1621f, 2.9754639f, -832.2728f), 1597), // Counter: 8839
        (new Vector3(294.8805f, 56.076904f, 640.2228f), 1596), // Counter: 8550
        (new Vector3(870.6644f, 95.68933f, -388.35742f), 1596), // Counter: 8493
        (new Vector3(642.96936f, 69.99304f, 407.79736f), 1596), // Counter: 8478
        (new Vector3(596.45984f, 70.29822f, 622.76636f), 1596), // Counter: 8475
        (new Vector3(-491.02008f, 2.9754639f, -529.59485f), 1596), // Counter: 8454
        (new Vector3(-158.64807f, 98.61902f, -132.73828f), 1596), // Counter: 8439
        (new Vector3(-682.7955f, 135.60681f, -195.26971f), 1597), // Counter: 8398
        (new Vector3(55.283447f, 111.31445f, -289.0822f), 1596), // Counter: 8380
        (new Vector3(471.18323f, 70.29822f, 530.022f), 1596), // Counter: 8336
        (new Vector3(788.8761f, 120.378296f, 109.391846f), 1596), // Counter: 8241
        (new Vector3(666.5292f, 79.11792f, -480.36932f), 1596), // Counter: 8227
        (new Vector3(475.73047f, 95.994385f, -87.08331f), 1596), // Counter: 8208
        (new Vector3(35.721313f, 65.11023f, 648.9509f), 1596), // Counter: 8099
        (new Vector3(-197.19238f, 74.906494f, 618.3412f), 1596), // Counter: 8050
        (new Vector3(354.1161f, 95.65869f, -288.92963f), 1596), // Counter: 7848
        (new Vector3(-648.0049f, 74.99805f, 403.95203f), 1596), // Counter: 7740
        (new Vector3(140.97803f, 55.98523f, 770.99243f), 1596), // Counter: 7670
        (new Vector3(433.70715f, 70.29822f, 683.52783f), 1596), // Counter: 7587
        (new Vector3(142.1073f, 16.403442f, -574.0597f), 1596), // Counter: 7492
        (new Vector3(-140.45929f, 22.354431f, -414.2672f), 1596), // Counter: 7401
        (new Vector3(386.92297f, 96.787964f, -451.37714f), 1596), // Counter: 7363
        (new Vector3(-729.427f, 4.989685f, -724.81885f), 1596), // Counter: 7314
        (new Vector3(245.59387f, 109.11719f, -18.173523f), 1596), // Counter: 7222
        (new Vector3(-394.88824f, 106.73682f, 175.43298f), 1596), // Counter: 7190
        (new Vector3(-661.7075f, 2.9754639f, -579.4919f), 1596), // Counter: 7138
        (new Vector3(-550.13354f, 106.98096f, 627.74084f), 1596), // Counter: 7044
        (new Vector3(-756.8322f, 76.55444f, 97.3678f), 1596), // Counter: 6962
        (new Vector3(-25.68097f, 102.22009f, 150.16394f), 1596), // Counter: 6888
        (new Vector3(-225.02484f, 74.99805f, 804.9896f), 1596), // Counter: 6864
        (new Vector3(835.08044f, 69.99304f, 699.09204f), 1596), // Counter: 6819
        (new Vector3(-884.123f, 3.7994385f, -682.0325f), 1596), // Counter: 6761
        (new Vector3(-676.41724f, 170.9773f, 640.37524f), 1596), // Counter: 6671
        (new Vector3(-343.16016f, 52.32312f, -382.1317f), 1596), // Counter: 6644
        (new Vector3(-729.9153f, 116.53308f, -79.05707f), 1596), // Counter: 6580
        (new Vector3(-713.80176f, 62.05847f, 192.61462f), 1596), // Counter: 6540
        (new Vector3(8.987488f, 103.196655f, 426.96265f), 1596), // Counter: 6520
        (new Vector3(490.40967f, 62.45508f, -590.56995f), 1596), // Counter: 6468
        (new Vector3(-716.1517f, 170.9773f, 794.4304f), 1596), // Counter: 6445
        (new Vector3(-451.6823f, 2.9754639f, -775.5703f), 1596), // Counter: 6423
        (new Vector3(-256.88562f, 120.98877f, 125.078125f), 1596), // Counter: 6315
        (new Vector3(-856.9619f, 68.833374f, -93.15637f), 1596), // Counter: 6251
        (new Vector3(617.08997f, 66.300415f, -703.8834f), 1596), // Counter: 6243
        (new Vector3(-798.24524f, 105.57703f, -310.5669f), 1597), // Counter: 6086
        (new Vector3(-784.7562f, 138.99438f, 699.7634f), 1596), // Counter: 5776
        (new Vector3(-585.2903f, 4.989685f, -864.8356f), 1596), // Counter: 5697
        (new Vector3(-600.27466f, 138.99438f, 802.6398f), 1596), // Counter: 5694
        (new Vector3(-767.4525f, 115.61755f, -235.00421f), 1596), // Counter: 5565
        (new Vector3(-729.5491f, 106.98096f, 561.1504f), 1596), // Counter: 5265
        (new Vector3(826.688f, 121.99585f, 434.9889f), 1596), // Counter: 5161
        (new Vector3(869.29126f, 109.97168f, 581.2008f), 1596), // Counter: 4980
        (new Vector3(381.73486f, 22.171326f, -743.64844f), 1596), // Counter: 4726
        (new Vector3(-680.5371f, 104.844604f, -354.78754f), 1596), // Counter: 4116
     ];
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