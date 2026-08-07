using TrackyTrack.Data;

namespace TrackyTrack;

public static class EnumHelper
{
    private static readonly HashSet<Territory> EurekaTerritories = [Territory.Pagos, Territory.Pyros, Territory.Hydatos];
    private static readonly HashSet<OccultTerritory> OccultTerritories = [OccultTerritory.SouthHorn, OccultTerritory.NorthHorn];

    public static bool PlayerInEureka()
        => EurekaTerritories.Contains((Territory)Plugin.ClientState.TerritoryType);

    public static bool PlayerInOccult()
        => OccultTerritories.Contains((OccultTerritory)Plugin.ClientState.TerritoryType);
}