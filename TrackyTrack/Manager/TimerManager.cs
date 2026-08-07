using System.Timers;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Utility;
using TrackyTrack.Data;

namespace TrackyTrack.Manager;

public class TimerManager
{
    private readonly Plugin Plugin;

    public ReductionResult LastReductionResult = new();
    private readonly Timer AwaitingReduction = new(200);

    public DesynthResultV2 LastDesynthesisResult = new();
    private readonly Timer AwaitingDesynthesis = new(200);

    public readonly Timer TicketUsedTimer = new(500);

    public uint Repaired;
    private readonly Timer RepairTimer = new(500);

    public readonly Dictionary<uint, Export.DutyLoot> LootCache = [];
    private readonly Timer LootTimer = new(500);

    public EurekaCoffer EurekaCoffer;
    private readonly Timer EurekaCofferTimer = new(600);

    public OccultCoffer OccultCoffer;
    private readonly Timer OccultTreasureTimer = new(300);

    public ushort LastBunnyFateId;
    public OccultPot OccultPot;
    private readonly Timer OccultCofferTimer = new(600);

    public TimerManager(Plugin plugin)
    {
        Plugin = plugin;

        AwaitingReduction.AutoReset = false;
        AwaitingReduction.Elapsed += StoreReductionResult;

        AwaitingDesynthesis.AutoReset = false;
        AwaitingDesynthesis.Elapsed += StoreDesynthesisResult;

        TicketUsedTimer.AutoReset = false;

        RepairTimer.AutoReset = false;
        RepairTimer.Elapsed += (_, _) => Repaired = 0;

        LootTimer.AutoReset = false;
        LootTimer.Elapsed += StoreLootResults;

        EurekaCofferTimer.AutoReset = false;
        EurekaCofferTimer.Elapsed += StoreEurekaResult;

        OccultTreasureTimer.AutoReset = false;
        OccultTreasureTimer.Elapsed += StoreOccultTreasure;

        OccultCofferTimer.AutoReset = false;
        OccultCofferTimer.Elapsed += StoreOccultBunny;
    }

    public void Dispose() { }

    public void StartReduction(uint source, uint collectability)
    {
        LastReductionResult = new ReductionResult(source, collectability) { AwaitingResults = true, };
        AwaitingReduction.Start();
    }

    public void StartDesynthesis(uint source)
    {
        LastDesynthesisResult = new DesynthResultV2(source) { AwaitingResults = true, };
        AwaitingDesynthesis.Start();
    }

    public void StartTicketUsed()
    {
        TicketUsedTimer.Start();
    }

    public void StartRepair()
    {
        RepairTimer.Start();
    }

    public void StartLoot()
    {
        LootTimer.Stop();
        LootTimer.Start();
    }

    public void StartEureka(uint lastBaseId)
    {
        EurekaCoffer = new EurekaCoffer(Plugin.ClientState.TerritoryType, lastBaseId) { AwaitingResults = true };

        EurekaCofferTimer.Stop();
        EurekaCofferTimer.Start();
    }

    public void StartOccultTreasure(uint baseId, Vector3 pos)
    {
        OccultCoffer = new OccultCoffer(Plugin.ClientState.TerritoryType, baseId, pos) { AwaitingResults = true };

        OccultTreasureTimer.Stop();
        OccultTreasureTimer.Start();
    }

    public void StartOccultPot(uint baseId, Vector3 pos)
    {
        OccultPot = new OccultPot(Plugin.ClientState.TerritoryType, baseId, pos, LastBunnyFateId) { AwaitingResults = true };

        OccultCofferTimer.Stop();
        OccultCofferTimer.Start();
    }

    public void RepairResult(int gilDifference)
    {
        if (!RepairTimer.Enabled)
            return;

        RepairTimer.Stop();

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.PlayerState.ContentId);
        character.Repairs += Repaired;
        character.RepairCost += (uint)gilDifference;

        Plugin.ConfigurationBase.SaveCharacterConfig();
    }

    private void StoreReductionResult(object? _, ElapsedEventArgs __)
    {
        var lastReduction = LastReductionResult.Clone();
        LastReductionResult = new ReductionResult();

        if (!lastReduction.IsValid)
            return;

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.PlayerState.ContentId);

        character.Reduction.History.Add(DateTime.Now, lastReduction);
        foreach (var result in lastReduction.Received.Where(r => r.Item != 0))
        {
            if (!character.Reduction.Total.TryAdd(result.Item, result.Count))
                character.Reduction.Total[result.Item] += result.Count;
        }

        Plugin.ConfigurationBase.SaveCharacterConfig();
        Plugin.UploadEntry(new Export.ReductionUpload(lastReduction));
    }

    private void StoreDesynthesisResult(object? _, ElapsedEventArgs __)
    {
        var lastDesynthesis = LastDesynthesisResult.Clone();
        LastDesynthesisResult = new DesynthResultV2();

        if (!lastDesynthesis.IsValid)
            return;

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.PlayerState.ContentId);

        character.Storage.History.Add(DateTime.Now, lastDesynthesis);
        foreach (var result in lastDesynthesis.Received)
        {
            if (!character.Storage.Total.TryAdd(result.Item, result.Count))
                character.Storage.Total[result.Item] += result.Count;
        }

        Plugin.ConfigurationBase.SaveCharacterConfig();
        Plugin.UploadEntry(new Export.DesynthesisResultV2(lastDesynthesis));
    }

    private static readonly uint[] TrackedCoffers = [32161, 36635, 36636, 41667];
    public void StoreCofferResult((uint ItemId, int Quantity)[] changes)
    {
        var added = changes.Where(pair => pair.Quantity > 0).ToArray();
        var removed = changes.Where(pair => pair.Quantity < 0).ToArray();
        if (added.Length != 1 || removed.Length != 1)
            return;

        var item = added[0];
        var coffer = removed[0];
        if (coffer.Quantity * -1 > 1)
            return;

        // Handle card packs just like any other lockbox
        if (Lockboxes.CardPacks.Contains(coffer.ItemId))
        {
            Plugin.LockboxHandler(coffer.ItemId, item.ItemId, (uint) item.Quantity);
            return;
        }

        if (!TrackedCoffers.Contains(coffer.ItemId))
            return;

        if (!Plugin.Configuration.EnableVentureCoffers && coffer.ItemId is 32161)
        {
            Plugin.Log.Warning("Opened venture coffer but has tracking disabled.");
            return;
        }

        if (!Plugin.Configuration.EnableGachaCoffers && coffer.ItemId is 36635 or 36636 or 41667)
        {
            Plugin.Log.Warning("Opened gacha coffer but has tracking disabled.");
            return;
        }

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.PlayerState.ContentId);
        if (coffer.ItemId == 32161 && VentureCoffer.Content.Contains(item.ItemId))
        {
            character.Coffer.Opened += 1;
            if (!character.Coffer.Obtained.TryAdd(item.ItemId, (uint) item.Quantity))
                character.Coffer.Obtained[item.ItemId] += (uint) item.Quantity;
        }
        else if (coffer.ItemId == 36635 && GachaThreeZero.Content.Contains(item.ItemId))
        {
            character.GachaThreeZero.Opened += 1;
            if (!character.GachaThreeZero.Obtained.TryAdd(item.ItemId, (uint) item.Quantity))
                character.GachaThreeZero.Obtained[item.ItemId] += (uint) item.Quantity;
        }
        else if (coffer.ItemId == 36636 && GachaFourZero.Content.Contains(item.ItemId))
        {
            character.GachaFourZero.Opened += 1;
            if (!character.GachaFourZero.Obtained.TryAdd(item.ItemId, (uint) item.Quantity))
                character.GachaFourZero.Obtained[item.ItemId] += (uint) item.Quantity;
        }
        else if (coffer.ItemId == 41667 && Sanctuary.Content.Contains(item.ItemId))
        {
            character.GachaSanctuary.Opened += 1;
            if (!character.GachaSanctuary.Obtained.TryAdd(item.ItemId, (uint) item.Quantity))
                character.GachaSanctuary.Obtained[item.ItemId] += (uint) item.Quantity;
        }
        else
        {
            Plugin.ChatGui.Print(Utils.SuccessMessage("You've found an unknown coffer drop."));
            Plugin.ChatGui.Print(Utils.SuccessMessage("Please consider sending the following information to the dev:"));
            Plugin.ChatGui.Print($"Coffer: {coffer.ItemId} Item: {item.ItemId}");

            return;
        }

        Plugin.ConfigurationBase.SaveCharacterConfig();
        Plugin.UploadEntry(new Export.GachaLoot(coffer.ItemId, item.ItemId, (uint) item.Quantity));
    }

    private void StoreEurekaResult(object? _, ElapsedEventArgs __)
    {
        var eurekaCoffer = EurekaCoffer.Clone();
        EurekaCoffer = new EurekaCoffer();

        if (!EnumHelper.PlayerInEureka())
            return;

        if (!eurekaCoffer.IsValid)
        {
            Plugin.Log.Warning("No items received, invalid result");
            return;
        }

        var result = new EurekaResult();
        foreach (var itemResult in eurekaCoffer.Received)
            result.Items.Add(new EurekaItem(itemResult.Item,itemResult.Count));

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.PlayerState.ContentId);
        character.Eureka.Opened += 1;
        character.Eureka.History[eurekaCoffer.Territory][eurekaCoffer.Rarity].Add(DateTime.Now, result);
        Plugin.ConfigurationBase.SaveCharacterConfig();

        Plugin.UploadEntry(new Export.BunnyLoot((uint)eurekaCoffer.Rarity, (uint)eurekaCoffer.Territory, eurekaCoffer.Received));
    }

    public void StoreOccultTreasure(object? _, ElapsedEventArgs __)
    {
        if (!EnumHelper.PlayerInOccult())
            return;

        var occultCoffer = OccultCoffer.Clone();
        OccultCoffer = new OccultCoffer();

        if (!occultCoffer.IsValid)
        {
            Plugin.Log.Warning("No items received, invalid result");
            return;
        }

        var result = new OccultResult();
        foreach (var itemResult in occultCoffer.Received)
            result.Items.Add(new OccultItem(itemResult.Item,itemResult.Count));

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.PlayerState.ContentId);
        character.Occult.TreasureOpened += 1;
        character.Occult.TreasureHistory[occultCoffer.Territory][occultCoffer.Rarity].Add(DateTime.Now, result);
        Plugin.ConfigurationBase.SaveCharacterConfig();

        Plugin.UploadEntry(new Export.OccultTreasure(occultCoffer));
    }

    public void StoreOccultBunny(object? _, ElapsedEventArgs __)
    {
        if (!EnumHelper.PlayerInOccult())
            return;

        var occultCoffer = OccultPot.Clone();
        OccultPot = new OccultPot();
        LastBunnyFateId = 0;

        if (!occultCoffer.IsValid)
        {
            Plugin.Log.Warning("No items received, invalid result");
            return;
        }

        var result = new OccultResult();
        foreach (var itemResult in occultCoffer.Received)
            result.Items.Add(new OccultItem(itemResult.Item,itemResult.Count));

        var character = Plugin.CharacterStorage.GetOrCreate(Plugin.PlayerState.ContentId);
        character.Occult.Opened += 1;
        character.Occult.History[occultCoffer.Territory][occultCoffer.Rarity].Add(DateTime.Now, result);
        Plugin.ConfigurationBase.SaveCharacterConfig();

        Plugin.UploadEntry(new Export.OccultBunny(occultCoffer));
    }

    private void StoreLootResults(object? _, ElapsedEventArgs __)
    {
        if (LootCache.Count == 0)
            return;

        foreach (var lootEntry in LootCache.Values)
            Plugin.UploadEntry(lootEntry);

        LootCache.Clear();
    }
}
