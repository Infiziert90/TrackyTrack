using System.Timers;
using TrackyTrack.Data;

namespace TrackyTrack.Manager;

public class TimerManager
{
    private readonly Plugin Plugin;

    private BulkResult LastBulkResult = new();
    private readonly Timer AwaitingBulkDesynth = new(1 * 500);

    public readonly Timer TicketUsedTimer = new(1 * 500);

    public uint Repaired;
    private readonly Timer RepairTimer = new(1 * 500);

    public int Revisited = -1;
    private readonly Timer RevisitTimer = new(1 * 3000);

    public TimerManager(Plugin plugin)
    {
        Plugin = plugin;

        AwaitingBulkDesynth.AutoReset = false;
        AwaitingBulkDesynth.Elapsed += StoreBulkResult;

        TicketUsedTimer.AutoReset = false;

        RepairTimer.AutoReset = false;
        RepairTimer.Elapsed += (_, _) => Repaired = 0;

        RevisitTimer.AutoReset = false;
        RevisitTimer.Elapsed += StoreRevisitResult;
    }

    public void Dispose() { }

    public void StartRevisit()
    {
        Plugin.TimerManager.Revisited = 0;
        RevisitTimer.Start();
    }


    public void StartBulk()
    {
        LastBulkResult = new();
        AwaitingBulkDesynth.Start();
    }

    public void StartTicketUsed()
    {
        TicketUsedTimer.Start();
    }

    public void StartRepair()
    {
        RepairTimer.Start();
    }

    public void StoreRevisitResult(object? _, ElapsedEventArgs __)
    {
        if (Revisited == -1)
            return;

        var result = new Export.RevisitedResult(Plugin.FrameworkManager.Type, Revisited != 0);
        Plugin.UploadEntry(result);
    }

    public void RepairResult(int gilDifference)
    {
        if (!RepairTimer.Enabled)
            return;

        RepairTimer.Stop();

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.ClientState.LocalContentId);
        character.Repairs += Repaired;
        character.RepairCost += (uint)gilDifference;

        Plugin.ConfigurationBase.SaveCharacterConfig();
    }

    public void DesynthItemAdded((uint ItemId, uint Quantity) changedItem)
    {
        if (!AwaitingBulkDesynth.Enabled)
            return;

        // 19 and below are crystals
        var isHQ = changedItem.ItemId > 1_000_000;
        var itemId = Utils.NormalizeItemId(changedItem.ItemId);

        if (itemId > 19)
            LastBulkResult.AddItem(itemId, changedItem.Quantity, isHQ);
        else
            LastBulkResult.AddCrystal(itemId, changedItem.Quantity);
    }

    public void DesynthItemRemoved((uint ItemId, uint Quantity) changedItem)
    {
        if (!AwaitingBulkDesynth.Enabled)
            return;

        // Impossible to bulk desynth collectables
        if (changedItem.ItemId is > 500_000 and < 1_000_000)
            return;

        var itemId = Utils.NormalizeItemId(changedItem.ItemId);
        LastBulkResult.AddSource(itemId);
    }

    public void StoreBulkResult(object? _, ElapsedEventArgs __)
    {
        if (!LastBulkResult.IsValid)
            return;

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.ClientState.LocalContentId);

        var desynthResult = new DesynthResult(LastBulkResult);
        character.Storage.History.Add(DateTime.Now, desynthResult);
        foreach (var result in LastBulkResult.Received.Where(r => r.Item != 0))
        {
            if (!character.Storage.Total.TryAdd(result.Item, result.Count))
                character.Storage.Total[result.Item] += result.Count;
        }

        Plugin.ConfigurationBase.SaveCharacterConfig();
        Plugin.UploadEntry(new Export.DesynthesisResult(desynthResult));
    }

    public static readonly uint[] TrackedCoffers = [32161, 36635, 36636, 41667];
    public void StoreCofferResult((uint ItemId, long Quantity)[] changes)
    {
        var added = changes.Where(pair => pair.Quantity > 0).ToArray();
        var removed = changes.Where(pair => pair.Quantity < 0).ToArray();
        if (added.Length != 1 || removed.Length != 1)
            return;

        var item = added[0];
        var coffer = removed[0];
        if (coffer.Quantity * -1 > 1)
            return;

        // Handle card packs just like any other gacha lockbox item
        if (Lockboxes.CardPacks.Contains(coffer.ItemId))
        {
            Plugin.LockboxHandler(coffer.ItemId, item.ItemId, (uint) item.Quantity);
            return;
        }

        if (!TrackedCoffers.Contains(coffer.ItemId))
            return;

        var save = false;
        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.ClientState.LocalContentId);
        if (Plugin.Configuration.EnableVentureCoffers)
        {
            if (coffer.ItemId == 32161 && VentureCoffer.Content.Contains(item.ItemId))
            {
                character.Coffer.Opened += 1;
                if (!character.Coffer.Obtained.TryAdd(item.ItemId, (uint) item.Quantity))
                    character.Coffer.Obtained[item.ItemId] += (uint) item.Quantity;
                save = true;
            }
        }

        if (Plugin.Configuration.EnableGachaCoffers)
        {
            if (coffer.ItemId == 36635 && GachaThreeZero.Content.Contains(item.ItemId))
            {
                character.GachaThreeZero.Opened += 1;
                if (!character.GachaThreeZero.Obtained.TryAdd(item.ItemId, (uint) item.Quantity))
                    character.GachaThreeZero.Obtained[item.ItemId] += (uint) item.Quantity;
                save = true;
            }
            else if (coffer.ItemId == 36636 && GachaFourZero.Content.Contains(item.ItemId))
            {
                character.GachaFourZero.Opened += 1;
                if (!character.GachaFourZero.Obtained.TryAdd(item.ItemId, (uint) item.Quantity))
                    character.GachaFourZero.Obtained[item.ItemId] += (uint) item.Quantity;
                save = true;
            }
            else if (coffer.ItemId == 41667 && Sanctuary.Content.Contains(item.ItemId))
            {
                character.GachaSanctuary.Opened += 1;
                if (!character.GachaSanctuary.Obtained.TryAdd(item.ItemId, (uint) item.Quantity))
                    character.GachaSanctuary.Obtained[item.ItemId] += (uint) item.Quantity;
                save = true;
            }
        }

        if (save)
        {
            Plugin.ConfigurationBase.SaveCharacterConfig();
            Plugin.UploadEntry(new Export.GachaLoot(coffer.ItemId, item.ItemId, (uint) item.Quantity));
            return;
        }

        Plugin.ChatGui.Print(Utils.SuccessMessage("You've found an unknown coffer drop."));
        Plugin.ChatGui.Print(Utils.SuccessMessage("Please consider sending the following information to the dev:"));
        Plugin.ChatGui.Print($"Coffer: {coffer.ItemId} Item: {item.ItemId}");
    }

    public void StoreEurekaResult((uint ItemId, long Quantity)[] changes)
    {
        if (!EurekaExtensions.AsArray.Contains(Plugin.ClientState.TerritoryType))
            return;

        var gil = changes.FirstOrDefault(c => c.ItemId == 1);
        if (gil.Quantity != 10_000 && gil.Quantity != 25_000 && gil.Quantity != 100_000)
            return;

        var result = new EurekaResult();
        foreach (var (itemId, quantity) in changes.Where(c => c.ItemId != 1))
        {
            if (quantity < 1)
            {
                Plugin.Log.Warning($"Eureka Result: {itemId} with {quantity}");
                return;
            }

            result.AddItem(itemId, (uint) quantity);
        }

        if (!result.IsValid)
        {
            Plugin.Log.Warning($"No items received, invalid result");
            return;
        }

        var rarity = EurekaExtensions.FromWorth(gil.Quantity);
        var territory = (Territory) Plugin.ClientState.TerritoryType;

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.ClientState.LocalContentId);
        character.Eureka.Opened += 1;
        character.Eureka.History[territory][rarity].Add(DateTime.Now, result);
        Plugin.ConfigurationBase.SaveCharacterConfig();

        Plugin.Log.Information($"Stored result for {rarity.ToName()} in {territory.ToName()}");
        foreach (var item in result.Items)
            Plugin.Log.Information($"Item: {item.Item} Quantity: {item.Count}");

        Plugin.UploadEntry(new Export.BunnyLoot((uint)rarity, (uint)territory, result.Items));
    }
}
