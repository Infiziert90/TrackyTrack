using Dalamud.Game.ClientState.Objects.SubKinds;
using TrackyTrack.Data;

namespace TrackyTrack;

[Serializable]
//From: https://github.com/MidoriKami/DailyDuty/
public class CharacterConfiguration
{
    // Increase with version bump
    public int Version { get; set; } = 1;

    public ulong LocalContentId;

    public string CharacterName = "";
    public string World = "Unknown";

    public uint Teleports = 0;
    public uint TeleportCost = 0;
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
    public uint Ventures = 0;
    public uint SackOfNuts = 0;
    public uint CenturioSeal = 0;
    public uint Bicolor = 0;
    public uint Skybuilder = 0;

    public Desynth Storage = new();
    public VentureCoffer Coffer = new();
    public GachaThreeZero GachaThreeZero = new();
    public GachaFourZero GachaFourZero = new();

    public EurekaTracker Eureka = new();

    public CharacterConfiguration() { }

    public CharacterConfiguration(ulong id, PlayerCharacter local)
    {
        LocalContentId = id;
        CharacterName = Utils.ToStr(local.Name);
        World = Utils.ToStr(local.HomeWorld.GameData!.Name);
    }

    public static CharacterConfiguration CreateNew() => new()
    {
        LocalContentId = Plugin.ClientState.LocalContentId,

        CharacterName = Plugin.ClientState.LocalPlayer?.Name.ToString() ?? "",
        World = Plugin.ClientState.LocalPlayer?.HomeWorld.GameData?.Name.ToString() ?? "Unknown"
    };

    public uint GetCurrencyCount(Currency currency)
    {
        return (currency) switch
        {
            Currency.SerpentSeals or Currency.FlameSeals or Currency.StormSeals => GCSeals,
            Currency.MGP => MGP,
            Currency.AlliedSeals => AlliedSeals,
            Currency.CenturioSeals => CenturioSeal,
            Currency.Bicolor => Bicolor,
            Currency.Skybuilders => Skybuilder,
            Currency.SackOfNuts => SackOfNuts,
            Currency.Ventures => Ventures,
            _ => 0
        };
    }
}
