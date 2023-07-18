using System.Timers;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using TrackyTrack.Data;

namespace TrackyTrack.Manager;

public class TimerManager
{
    private Plugin Plugin;

    private BulkResult LastBulkResult = new();
    private Timer FinishedBulkDesynth = new(1 * 1000);

    private Timer CastTimer = new(3 * 1000);
    private bool OpeningCoffer;

    private Timer RepairTimer = new(0.5 * 1000);
    public uint GilCount;
    public uint Repaired;

    public TimerManager(Plugin plugin)
    {
        Plugin = plugin;

        FinishedBulkDesynth.AutoReset = false;
        FinishedBulkDesynth.Elapsed += StoreBulkResult;

        CastTimer.AutoReset = false;
        CastTimer.Elapsed += (_, _) => OpeningCoffer = false;

        RepairTimer.AutoReset = false;
        RepairTimer.Elapsed += (_, _) => Repaired = 0;

        Plugin.Framework.Update += RegisterRepair;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= RegisterRepair;
    }

    public unsafe void RegisterRepair(Framework _)
    {
        if (!Plugin.Configuration.EnableRepair)
            return;

        var instance = InventoryManager.Instance();
        if (instance == null)
            return;

        var container = instance->GetInventoryContainer(InventoryType.Currency);
        var currentGil = container->Items[0].Quantity;

        if (currentGil < GilCount)
            RepairResult(currentGil);

        GilCount = currentGil;
    }

    public void StartBulk()
    {
        LastBulkResult = new();
        FinishedBulkDesynth.Start();
    }

    public void StartCast()
    {
        CastTimer.Stop();
        CastTimer.Start();

        OpeningCoffer = true;
    }

    public void StartRepair()
    {
        RepairTimer.Start();
    }

    public void RepairResult(uint currentGil)
    {
        if (!RepairTimer.Enabled)
            return;

        RepairTimer.Stop();

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.ClientState.LocalContentId);
        character.Repairs += Repaired;
        character.RepairCost += GilCount - currentGil;

        Plugin.ConfigurationBase.SaveCharacterConfig();
    }

    public void DesynthItemAdded((uint ItemId, InventoryItem.ItemFlags Flags, ulong CharacterId, uint Quantity) item)
    {
        if (!FinishedBulkDesynth.Enabled)
            return;

        // 19 and below are crystals
        if (item.ItemId > 19)
            LastBulkResult.AddItem(item.ItemId, item.Quantity, item.Flags == InventoryItem.ItemFlags.HQ);
        else
            LastBulkResult.AddCrystal(item.ItemId, item.Quantity);
    }

    public void DesynthItemRemoved((uint ItemId, InventoryItem.ItemFlags Flags, ulong CharacterId, uint Quantity) item)
    {
        if (!FinishedBulkDesynth.Enabled)
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

    public void StoreCofferResult((uint ItemId, InventoryItem.ItemFlags Flags, ulong CharacterId, uint Quantity) item)
    {
        if (!OpeningCoffer)
            return;

        var save = false;
        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.ClientState.LocalContentId);
        if (VentureCoffer.Content.Contains(item.ItemId) && Plugin.Configuration.EnableVentureCoffers)
        {
            character.Coffer.Opened += 1;
            if (!character.Coffer.Obtained.TryAdd(item.ItemId, item.Quantity))
                character.Coffer.Obtained[item.ItemId] += item.Quantity;
            save = true;
        }
        else if (GachaThreeZero.Content.Contains(item.ItemId) && Plugin.Configuration.EnableGachaCoffers)
        {
            character.GachaThreeZero.Opened += 1;
            if (!character.GachaThreeZero.Obtained.TryAdd(item.ItemId, item.Quantity))
                character.GachaThreeZero.Obtained[item.ItemId] += item.Quantity;
            save = true;
        }
        else if (GachaFourZero.Content.Contains(item.ItemId) && Plugin.Configuration.EnableGachaCoffers)
        {
            character.GachaFourZero.Opened += 1;
            if (!character.GachaFourZero.Obtained.TryAdd(item.ItemId, item.Quantity))
                character.GachaFourZero.Obtained[item.ItemId] += item.Quantity;
            save = true;
        }

        if (OpeningCoffer)
            Plugin.ChatGui.Print($"[TrackyTrack] Found an item that is possible from chest {item.ItemId} but in no list");

        if (save)
        {
            OpeningCoffer = false;
            Plugin.ConfigurationBase.SaveCharacterConfig();
        }
    }
}
