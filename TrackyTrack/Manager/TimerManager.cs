using System.Timers;
using CriticalCommonLib.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using TrackyTrack.Data;

namespace TrackyTrack.Manager;

public class TimerManager
{
    private Plugin Plugin;

    private BulkResult LastBulkResult = new();
    private readonly Timer AwaitingBulkDesynth = new(1 * 1000);

    private readonly Timer CastTimer = new(3 * 1000);
    private bool OpeningCoffer;

    public readonly Timer TicketUsedTimer = new(1 * 1000);

    private readonly Timer RepairTimer = new(0.5 * 1000);
    public uint Repaired;

    private Territory EurekaTerritory;
    private CofferRarity EurekaRarity;
    private EurekaResult EurekaResult = new();
    public readonly Timer AwaitingEurekaResult = new(1 * 1000);

    public TimerManager(Plugin plugin)
    {
        Plugin = plugin;

        AwaitingBulkDesynth.AutoReset = false;
        AwaitingBulkDesynth.Elapsed += StoreBulkResult;

        CastTimer.AutoReset = false;
        CastTimer.Elapsed += (_, _) => OpeningCoffer = false;

        TicketUsedTimer.AutoReset = false;

        RepairTimer.AutoReset = false;
        RepairTimer.Elapsed += (_, _) => Repaired = 0;

        AwaitingEurekaResult.AutoReset = false;
        AwaitingEurekaResult.Elapsed += StoreEurekaResult;
    }

    public void Dispose() { }

    public void StartBulk()
    {
        LastBulkResult = new();
        AwaitingBulkDesynth.Start();
    }

    public void StartCoffer()
    {
        CastTimer.Stop();
        CastTimer.Start();

        OpeningCoffer = true;
    }

    public void StartTicketUsed()
    {
        TicketUsedTimer.Start();
    }

    public void StartRepair()
    {
        RepairTimer.Start();
    }

    public void StartEureka(uint rarity)
    {
        EurekaTerritory = (Territory) Plugin.ClientState.TerritoryType;
        EurekaRarity = (CofferRarity) rarity;
        EurekaResult = new();
        AwaitingEurekaResult.Start();
    }

    public void RepairResult(int gilDifference)
    {
        if (!RepairTimer.Enabled)
            return;

        RepairTimer.Stop();

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.ClientState.LocalContentId);
        character.Repairs += Repaired;
        character.RepairCost += (uint) gilDifference;

        Plugin.ConfigurationBase.SaveCharacterConfig();
    }

    public void DesynthItemAdded(InventoryMonitor.ItemChangesItem item)
    {
        if (!AwaitingBulkDesynth.Enabled)
            return;

        // 19 and below are crystals
        if (item.ItemId > 19)
            LastBulkResult.AddItem(item.ItemId, (uint) item.Quantity, item.Flags == InventoryItem.ItemFlags.HQ);
        else
            LastBulkResult.AddCrystal(item.ItemId, (uint) item.Quantity);
    }

    public void DesynthItemRemoved(InventoryMonitor.ItemChangesItem item)
    {
        if (!AwaitingBulkDesynth.Enabled)
            return;

        LastBulkResult.AddSource(item.ItemId);
    }

    public void StoreBulkResult(object? _, ElapsedEventArgs __)
    {
        if (!LastBulkResult.IsValid)
            return;

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.ClientState.LocalContentId);

        character.Storage.History.Add(DateTime.Now, new DesynthResult(LastBulkResult));
        foreach (var result in LastBulkResult.Received.Where(r => r.Item != 0))
        {
            var id = result.Item > 1_000_000 ? result.Item - 1_000_000 : result.Item;
            if (!character.Storage.Total.TryAdd(id, result.Count))
                character.Storage.Total[id] += result.Count;
        }

        Plugin.ConfigurationBase.SaveCharacterConfig();
    }

    public void StoreCofferResult(InventoryMonitor.ItemChangesItem item)
    {
        if (!OpeningCoffer)
            return;

        var save = false;
        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.ClientState.LocalContentId);
        if (VentureCoffer.Content.Contains(item.ItemId) && Plugin.Configuration.EnableVentureCoffers)
        {
            character.Coffer.Opened += 1;
            if (!character.Coffer.Obtained.TryAdd(item.ItemId, (uint) item.Quantity))
                character.Coffer.Obtained[item.ItemId] += (uint) item.Quantity;
            save = true;
        }
        else if (GachaThreeZero.Content.Contains(item.ItemId) && Plugin.Configuration.EnableGachaCoffers)
        {
            character.GachaThreeZero.Opened += 1;
            if (!character.GachaThreeZero.Obtained.TryAdd(item.ItemId, (uint) item.Quantity))
                character.GachaThreeZero.Obtained[item.ItemId] += (uint) item.Quantity;
            save = true;
        }
        else if (GachaFourZero.Content.Contains(item.ItemId) && Plugin.Configuration.EnableGachaCoffers)
        {
            character.GachaFourZero.Opened += 1;
            if (!character.GachaFourZero.Obtained.TryAdd(item.ItemId, (uint) item.Quantity))
                character.GachaFourZero.Obtained[item.ItemId] += (uint) item.Quantity;
            save = true;
        }

        if (save)
        {
            OpeningCoffer = false;
            Plugin.ConfigurationBase.SaveCharacterConfig();
        }

        if (OpeningCoffer)
            Plugin.ChatGui.Print($"[TrackyTrack] Found an item that is possible from chest {item.ItemId} but in no list");
    }

    public void EurekaItemAdded(InventoryMonitor.ItemChangesItem item)
    {
        if (!AwaitingEurekaResult.Enabled)
            return;

        EurekaResult.AddItem(item.ItemId, (uint) item.Quantity);
    }

    public void StoreEurekaResult(object? _, ElapsedEventArgs __)
    {
        if (!EurekaResult.IsValid)
            return;

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.ClientState.LocalContentId);
        character.Eureka.History[EurekaTerritory][EurekaRarity].Add(DateTime.Now, EurekaResult);
        character.Eureka.Opened += 1;

        Plugin.ConfigurationBase.SaveCharacterConfig();
    }
}
