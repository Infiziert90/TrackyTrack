using Dalamud.Game.ClientState.Statuses;

namespace TrackyTrack.Data;

[Serializable]
public enum TeleportBuff : uint
{
    None = 0,

    // Free company actions:
    ReducedRatesI = 1,   // 20% discount
    ReducedRatesII = 2,  // 30% discount
    ReducedRatesIII = 3, // 40% discount
}

public static class TeleportBuffExtension
{
    public static string ToName(this TeleportBuff buff)
    {
        return (buff) switch
        {
            TeleportBuff.None => "None",
            TeleportBuff.ReducedRatesI => "Reduced Rates (20%)",
            TeleportBuff.ReducedRatesII => "Reduced Rates II (30%)",
            TeleportBuff.ReducedRatesIII => "Reduced Rates III (40%)",
            _ => "Unknown"
        };
    }

    public static TeleportBuff FromStatusList(StatusList statusList)
    {
        foreach (var item in statusList)
        {
            // "Reduced Rates"
            if (item.StatusId == 364)
            {
                switch (item.StackCount)
                {
                    case 40:
                        return TeleportBuff.ReducedRatesIII;
                    case 30:
                        return TeleportBuff.ReducedRatesII;
                    case 20:
                        return TeleportBuff.ReducedRatesI;
                }
            }
        }

        return TeleportBuff.None;
    }

    public static uint ToOriginalCost(this TeleportBuff buff, uint discountedCost)
    {
        return (buff) switch
        {
            // I think FFXIV rounds down to the nearest gil when discounting,
            // so round up when calculating original cost
            TeleportBuff.ReducedRatesI => (uint)Math.Ceiling(discountedCost / 0.8),
            TeleportBuff.ReducedRatesII => (uint)Math.Ceiling(discountedCost / 0.7),
            TeleportBuff.ReducedRatesIII => (uint)Math.Ceiling(discountedCost / 0.6),
            _ => discountedCost
        };
    }
}
