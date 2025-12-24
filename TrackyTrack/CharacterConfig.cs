using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Collections.Concurrent;
using TrackyTrack.Data;

namespace TrackyTrack;

[Serializable]
//From: https://github.com/MidoriKami/DailyDuty/
public class CharacterConfiguration
{
    // Increase with version bump
    public int Version { get; set; } = 3;

    public ulong LocalContentId;

    public string CharacterName = "";
    public string World = "Unknown";

    public uint Teleports = 0;
    public uint TeleportCost = 0;
    public ConcurrentDictionary<TeleportBuff, uint> TeleportsWithBuffs = new();
    // Teleport savings (original cost - discounted cost) for each buff type
    public ConcurrentDictionary<TeleportBuff, uint> TeleportSavingsWithBuffs = new();
    // Tickets
    public uint TeleportsAetheryte = 0;
    public uint TeleportsGC = 0;
    public uint TeleportsVesperBay = 0;
    public uint TeleportsFirmament = 0;

    public uint Repairs = 0;
    public uint RepairCost = 0;

    // Currency
    public uint GCSeals = 0;
    public uint MGP = 0;
    public uint AlliedSeals = 0;
    public uint VentureCoins = 0;
    public uint SackOfNuts = 0;
    public uint CenturioSeal = 0;
    public uint Bicolor = 0;
    public uint Skybuilder = 0;

    public Desynth Storage = new();
    public VentureCoffer Coffer = new();
    public GachaThreeZero GachaThreeZero = new();
    public GachaFourZero GachaFourZero = new();
    public Sanctuary GachaSanctuary = new();
    public Retainer VentureStorage = new();
    public EurekaTracker Eureka = new();
    public OccultTracker Occult = new();
    public Lockboxes Lockbox = new();
    public MiniCactpot MiniCactpot = new();

    public CharacterConfiguration() { }

    public CharacterConfiguration(ulong id, IPlayerCharacter local)
    {
        LocalContentId = id;
        CharacterName = local.Name.TextValue;
        World = local.HomeWorld.Value.Name.ToString();
    }

    public static CharacterConfiguration CreateNew() => new()
    {
        LocalContentId = Plugin.PlayerState.ContentId,

        CharacterName = Plugin.PlayerState.CharacterName,
        World = Plugin.PlayerState.HomeWorld.Value.Name.ToString()
    };

    public uint GetCurrencyCount(Currency currency)
    {
        return currency switch
        {
            Currency.SerpentSeals or Currency.FlameSeals or Currency.StormSeals => GCSeals,
            Currency.MGP => MGP,
            Currency.AlliedSeals => AlliedSeals,
            Currency.CenturioSeals => CenturioSeal,
            Currency.Bicolor => Bicolor,
            Currency.Skybuilders => Skybuilder,
            Currency.SackOfNuts => SackOfNuts,
            Currency.Ventures => VentureCoins,
            _ => 0
        };
    }
}
