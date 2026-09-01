using FFXIVClientStructs.FFXIV.Client.Game;
using Newtonsoft.Json;

namespace TrackyTrack.Data;

public class Roulette
{
    public uint Total;
    public Dictionary<DateTime, RouletteData> History = new();
}

public class RouletteData
{
    public uint Roulette;
    public uint CFC;

    public uint Class;
    public int Level;
    public int ClassExp;

    public uint Exp;
    public uint Gil;

    public bool GotBonus;

    public bool LimitedLeveling;
    public bool InProgress;

    [JsonIgnore]
    public bool AwaitingResults;

    [JsonIgnore]
    public bool IsValid => GotBonus && Roulette > 0 && Level > 0 && Class > 0 && Exp > 0;

    public unsafe void AddBonus(uint exp, uint gil)
    {
        CFC = GameMain.Instance()->CurrentContentFinderConditionId;

        GotBonus = true;
        Exp = exp;
        Gil = gil;
    }

    public RouletteData Clone()
        => new()
        {
            Roulette = Roulette, CFC = CFC, Class = Class, Level = Level, Exp = Exp, Gil = Gil, ClassExp = ClassExp,
            GotBonus = GotBonus, LimitedLeveling = LimitedLeveling, InProgress = InProgress,
        };
}