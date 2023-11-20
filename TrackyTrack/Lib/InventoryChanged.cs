using System.Threading.Tasks;
using CriticalCommonLib;
using CriticalCommonLib.Enums;
using CriticalCommonLib.Extensions;
using CriticalCommonLib.Models;
using CriticalCommonLib.Services;
using CriticalCommonLib.Services.Ui;
using CriticalCommonLib.Time;
using static CriticalCommonLib.Services.InventoryMonitor;
using static FFXIVClientStructs.FFXIV.Client.Game.InventoryItem;

namespace TrackyTrack.Lib;

// Extracted from CriticalLib->InventoryMonitor with just specific inventory type scanning
public class InventoryChanged
{
    public OdrScanner OdrScanner { get; private set; } = null!;
    public IGameUiManager GameUi { get; private set; } = null!;
    public IGameInterface GameInterface { get; private set; } = null!;
    public IInventoryScanner InventoryScanner { get; private set; } = null!;
    public ICharacterMonitor CharacterMonitor { get; private set; } = null!;

    private readonly Dictionary<ulong, Inventory> CachedInventories = new();
    private Dictionary<(uint, ItemFlags, ulong), int> ItemCounts = new();

    public event ItemAddedEvent? OnItemAdded;
    public delegate void ItemAddedEvent(ItemChangesItem changedItem);

    public event ItemRemovedEvent? OnItemRemoved;
    public delegate void ItemRemovedEvent(ItemChangesItem changedItem);

    public void Initialize()
    {
        Plugin.PluginInterface.Create<Service>();
        Service.SeTime = new SeTime();
        Service.ExcelCache = new ExcelCache(Plugin.Data, false, false, false);
        Service.ExcelCache.PreCacheItemData();

        GameInterface = new GameInterface(Plugin.Hook);
        CharacterMonitor = new CharacterMonitor(Plugin.Framework, Plugin.ClientState, Service.ExcelCache);

        GameUi = new GameUiManager(Plugin.Hook);
        OdrScanner = new OdrScanner(CharacterMonitor);

        InventoryScanner = new InventoryScanner(CharacterMonitor, GameUi, GameInterface, OdrScanner, Plugin.Hook);
        InventoryScanner.Enable();

        InventoryScanner.BagsChanged += BagsChangedTrigger;
        CharacterMonitor.OnCharacterRemoved += OnCharacterRemoved;
        Plugin.ClientState.Login += LoadOnLogin;

        if (Plugin.ClientState.IsLoggedIn)
            LoadData();
    }

    private void OnCharacterRemoved(ulong characterId)
    {
        if (CachedInventories.ContainsKey(characterId))
            ClearCharacterInventories(characterId);
    }

    public void ClearCharacterInventories(ulong characterId)
    {
        if (CachedInventories.TryGetValue(characterId, out var inventory))
        {
            InventoryScanner.ClearRetainerCache(characterId);
            inventory.ClearInventories();
        }
    }


    public void LoadOnLogin() => LoadData();
    public void LoadData()
    {
        var characterId = Plugin.ClientState.LocalContentId;
        if (characterId == 0)
            return;

        GenerateItemCounts();
        if (!CachedInventories.ContainsKey(characterId))
            CachedInventories[characterId] = new Inventory(CharacterType.Character, characterId);

        var inventory = CachedInventories[characterId];
        var inventoryChanges = new List<InventoryChange>();

        GenerateCharacterInventories(inventory, inventoryChanges);
        GenerateArmouryChestInventories(inventory, inventoryChanges);
        GenerateCrystalInventories(inventory, inventoryChanges);

        GenerateItemCounts();
    }

    private void BagsChangedTrigger(List<BagChange> _)
    {
        Task.Run(GenerateInventoriesTask);
    }

    private void GenerateInventoriesTask()
    {
        var characterId = Plugin.ClientState.LocalContentId;
        if (characterId == 0)
            return;

        GenerateItemCounts();
        var oldItemCounts = ItemCounts;

        if (!CachedInventories.ContainsKey(characterId))
            CachedInventories[characterId] = new Inventory(CharacterType.Character, characterId);

        var inventory = CachedInventories[characterId];
        var inventoryChanges = new List<InventoryChange>();

        GenerateCharacterInventories(inventory, inventoryChanges);
        GenerateArmouryChestInventories(inventory, inventoryChanges);
        GenerateCrystalInventories(inventory, inventoryChanges);

        GenerateItemCounts();
        var newItemCounts = ItemCounts;
        var itemChanges = CompareItemCounts(oldItemCounts, newItemCounts);
        Plugin.Framework.RunOnFrameworkThread(() => { TriggerInventoryChanged(inventoryChanges, itemChanges); });
    }

    public void GenerateItemCounts()
    {
        var retainerItemCounts = new Dictionary<(uint, ItemFlags, ulong), int>();
        var itemCounts = new Dictionary<(uint, ItemFlags), int>();
        foreach (var inventory in CachedInventories)
        {
            foreach (var itemList in inventory.Value.GetAllInventories())
            {
                foreach (var item in itemList)
                {
                    if (item == null)
                        continue;

                    var key = (item.ItemId, item.Flags, item.RetainerId);
                    retainerItemCounts.TryAdd(key, 0);
                    retainerItemCounts[key] += (int)item.Quantity;

                    var key2 = (item.ItemId, item.Flags);
                    itemCounts.TryAdd(key2, 0);
                    itemCounts[key2] += (int)item.Quantity;

                }
            }
        }
        ItemCounts = retainerItemCounts;
    }

    private void GenerateCharacterInventories(Inventory inventory, List<InventoryChange> inventoryChanges)
    {
        if (InventoryScanner.InMemory.Contains(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory2) &&
            InventoryScanner.InMemory.Contains(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory3) &&
            InventoryScanner.InMemory.Contains(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory1) &&
            InventoryScanner.InMemory.Contains(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory4))
        {
            var bag1 = InventoryScanner.CharacterBag1;
            var bag2 = InventoryScanner.CharacterBag2;
            var bag3 = InventoryScanner.CharacterBag3;
            var bag4 = InventoryScanner.CharacterBag4;
            inventory.LoadGameItems(bag1, InventoryType.Bag0, InventoryCategory.CharacterBags, false, inventoryChanges);
            inventory.LoadGameItems(bag2, InventoryType.Bag1, InventoryCategory.CharacterBags, false, inventoryChanges);
            inventory.LoadGameItems(bag3, InventoryType.Bag2, InventoryCategory.CharacterBags, false, inventoryChanges);
            inventory.LoadGameItems(bag4, InventoryType.Bag3, InventoryCategory.CharacterBags, false, inventoryChanges);
        }
    }

    private void GenerateArmouryChestInventories(Inventory inventory, List<InventoryChange> inventoryChanges)
    {
        var inventoryTypes = new HashSet<FFXIVClientStructs.FFXIV.Client.Game.InventoryType>();
        inventoryTypes.Add( FFXIVClientStructs.FFXIV.Client.Game.InventoryType.ArmoryMainHand);
        inventoryTypes.Add(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.ArmoryHead);
        inventoryTypes.Add(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.ArmoryBody);
        inventoryTypes.Add(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.ArmoryHands);
        inventoryTypes.Add(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.ArmoryLegs);
        inventoryTypes.Add(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.ArmoryFeets);
        inventoryTypes.Add(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.ArmoryOffHand);
        inventoryTypes.Add(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.ArmoryEar);
        inventoryTypes.Add(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.ArmoryNeck);
        inventoryTypes.Add(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.ArmoryWrist);
        inventoryTypes.Add(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.ArmoryRings);
        inventoryTypes.Add(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.ArmorySoulCrystal);
        foreach (var inventoryType in inventoryTypes)
        {
            if (!InventoryScanner.InMemory.Contains(inventoryType))
            {
                return;
            }
        }

        var gearSets = InventoryScanner.GetGearSets();
        foreach (var inventoryType in inventoryTypes)
        {
            if (!InventoryScanner.InMemory.Contains(inventoryType))
            {
                continue;
            }
            var armoryItems = InventoryScanner.GetInventoryByType(inventoryType);
            inventory.LoadGameItems(armoryItems, inventoryType.Convert(), InventoryCategory.CharacterArmoryChest, false, inventoryChanges,
                (newItem,_) =>
            {
                if(gearSets.ContainsKey(newItem.ItemId))
                {
                    newItem.GearSets = gearSets[newItem.ItemId].Select(c => (uint)c.Item1).ToArray();
                    newItem.GearSetNames = gearSets[newItem.ItemId].Select(c => c.Item2).ToArray();
                }
                else if(gearSets.ContainsKey(newItem.ItemId + 1_000_000))
                {
                    newItem.GearSets = gearSets[newItem.ItemId + 1_000_000].Select(c => (uint)c.Item1).ToArray();
                    newItem.GearSetNames = gearSets[newItem.ItemId + 1_000_000].Select(c => c.Item2).ToArray();
                }
                else
                {
                    newItem.GearSets = new uint[]{};
                }
            });
        }
    }

    private void GenerateCrystalInventories(Inventory inventory, List<InventoryChange> inventoryChanges)
    {
        var inventoryTypes = new HashSet<FFXIVClientStructs.FFXIV.Client.Game.InventoryType>();
        inventoryTypes.Add( FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Crystals);
        foreach (var inventoryType in inventoryTypes)
        {
            if (!InventoryScanner.InMemory.Contains(inventoryType))
            {
                return;
            }
        }

        foreach (var inventoryType in inventoryTypes)
        {
            var items = InventoryScanner.GetInventoryByType(inventoryType);
            var inventoryCategory = inventoryType.Convert().ToInventoryCategory();
            inventory.LoadGameItems(items, inventoryType.Convert(), inventoryCategory, false, inventoryChanges);
        }
    }

    private static ItemChanges CompareItemCounts(Dictionary<(uint, ItemFlags, ulong), int> oldItemCounts, Dictionary<(uint, ItemFlags, ulong), int> newItemCounts)
    {
        var newItems = new Dictionary<(uint, ItemFlags, ulong), int>();
        var removedItems = new Dictionary<(uint, ItemFlags, ulong), int>();

        foreach (var oldItem in oldItemCounts)
        {
            if (newItemCounts.ContainsKey(oldItem.Key))
            {
                if (newItemCounts[oldItem.Key] != oldItem.Value)
                {
                    var relativeCount = newItemCounts[oldItem.Key] - oldItem.Value;
                    if (relativeCount > 0)
                    {
                        newItems.Add(oldItem.Key, relativeCount);
                    }
                    else
                    {
                        removedItems.Add(oldItem.Key, Math.Abs(relativeCount));
                    }
                }
            }
            else
            {
                removedItems.Add(oldItem.Key, oldItem.Value);
            }
        }

        foreach (var newItem in newItemCounts)
            if (!oldItemCounts.ContainsKey(newItem.Key))
                newItems.Add(newItem.Key, newItem.Value);

        var actualAddedItems = new List<ItemChangesItem>();
        var actualDeletedItems = new List<ItemChangesItem>();

        foreach (var newItem in newItems)
            actualAddedItems.Add(ConvertHashedItem(newItem.Key, newItem.Value));

        foreach (var removedItem in removedItems)
            actualDeletedItems.Add(ConvertHashedItem(removedItem.Key, removedItem.Value));

        return new ItemChanges( actualAddedItems, actualDeletedItems);
    }

    private static ItemChangesItem ConvertHashedItem((uint, ItemFlags, ulong) itemHash, int quantity)
    {
        return new ItemChangesItem
        {
            ItemId = itemHash.Item1,
            Flags = itemHash.Item2,
            CharacterId = itemHash.Item3,
            Quantity = quantity,
            Date = DateTime.Now
        };
    }

    public void TriggerInventoryChanged(List<InventoryChange> _, ItemChanges? changedItems)
    {
        if (changedItems != null)
        {
            foreach (var changedItem in changedItems.NewItems)
                if (changedItem.ItemId != 1)
                    OnItemAdded?.Invoke(changedItem);

            foreach (var changedItem in changedItems.RemovedItems)
                if (changedItem.ItemId != 1)
                    OnItemRemoved?.Invoke(changedItem);
        }
    }

    public void Dispose()
    {
        try
        {
            InventoryScanner.BagsChanged -= BagsChangedTrigger;
            CharacterMonitor.OnCharacterRemoved -= OnCharacterRemoved;
            Plugin.ClientState.Login -= LoadOnLogin;

            InventoryScanner.Dispose();
            OdrScanner.Dispose();
            GameUi.Dispose();
            CharacterMonitor.Dispose();
            GameInterface.Dispose();
            Service.Dereference();
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Dispose wasn't possible");
            // Init went wrong, we disposed what was possible
        }
    }
}
