using System.Threading.Tasks;
using CriticalCommonLib.Enums;
using CriticalCommonLib.Extensions;
using CriticalCommonLib.Models;
using CriticalCommonLib.Services;
using Dalamud.Logging;
using static CriticalCommonLib.Services.InventoryMonitor;
using static FFXIVClientStructs.FFXIV.Client.Game.InventoryItem;

namespace TrackyTrack.Lib;

public static class InventoryChanged
{
    private static readonly Dictionary<ulong, Inventory> CachedInventories;
    private static Dictionary<(uint, ItemFlags, ulong), int> ItemCounts = new();

    public record EventSubscriber(EventType Type, Action<(uint, ItemFlags, ulong, uint)> Action);
    public static readonly Dictionary<string, EventSubscriber> Subscribed = new();

    static InventoryChanged()
    {
        CachedInventories = new Dictionary<ulong, Inventory>();

        Plugin.InventoryScanner.BagsChanged += BagsChangedTrigger;
        Plugin.CharacterMonitor.OnCharacterRemoved += OnCharacterRemoved;
        Plugin.ClientState.Login += LoadOnLogin;

        if (Plugin.ClientState.IsLoggedIn)
            LoadData();
    }

    private static void OnCharacterRemoved(ulong characterId)
    {
        if (CachedInventories.ContainsKey(characterId))
            ClearCharacterInventories(characterId);
    }

    public static void ClearCharacterInventories(ulong characterId)
    {
        if (CachedInventories.TryGetValue(characterId, out var inventory))
        {
            Plugin.InventoryScanner.ClearRetainerCache(characterId);
            inventory.ClearInventories();
        }
    }


    public static void LoadOnLogin(object? _, EventArgs __) => LoadData();
    public static void LoadData()
    {
        PluginLog.Information("OnLogin triggered");
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

        GenerateItemCounts();
    }

    public static void FillEmptySlots()
    {
        foreach (var inventory in CachedInventories)
        {
            inventory.Value.FillSlots();
        }
    }

    private static void BagsChangedTrigger(List<BagChange> _)
    {
        Task.Run(GenerateInventoriesTask);
    }

    private static void GenerateInventoriesTask()
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

        GenerateItemCounts();
        var newItemCounts = ItemCounts;
        var itemChanges = CompareItemCounts(oldItemCounts, newItemCounts);
        Plugin.Framework.RunOnFrameworkThread(() => { TriggerInventoryChanged(inventoryChanges, itemChanges); });
    }

    public static void GenerateItemCounts()
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

    private static void GenerateCharacterInventories(Inventory inventory, List<InventoryChange> inventoryChanges)
    {
        if (Plugin.InventoryScanner.InMemory.Contains(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory2) &&
            Plugin.InventoryScanner.InMemory.Contains(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory3) &&
            Plugin.InventoryScanner.InMemory.Contains(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory1) &&
            Plugin.InventoryScanner.InMemory.Contains(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory4))
        {
            var bag1 = Plugin.InventoryScanner.CharacterBag1;
            var bag2 = Plugin.InventoryScanner.CharacterBag2;
            var bag3 = Plugin.InventoryScanner.CharacterBag3;
            var bag4 = Plugin.InventoryScanner.CharacterBag4;
            inventory.LoadGameItems(bag1, InventoryType.Bag0, InventoryCategory.CharacterBags, false, inventoryChanges);
            inventory.LoadGameItems(bag2, InventoryType.Bag1, InventoryCategory.CharacterBags, false, inventoryChanges);
            inventory.LoadGameItems(bag3, InventoryType.Bag2, InventoryCategory.CharacterBags, false, inventoryChanges);
            inventory.LoadGameItems(bag4, InventoryType.Bag3, InventoryCategory.CharacterBags, false, inventoryChanges);
        }
    }

    private static void GenerateArmouryChestInventories(Inventory inventory, List<InventoryChange> inventoryChanges)
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
            if (!Plugin.InventoryScanner.InMemory.Contains(inventoryType))
            {
                return;
            }
        }

        var gearSets = Plugin.InventoryScanner.GetGearSets();
        foreach (var inventoryType in inventoryTypes)
        {
            if (!Plugin.InventoryScanner.InMemory.Contains(inventoryType))
            {
                continue;
            }
            var armoryItems = Plugin.InventoryScanner.GetInventoryByType(inventoryType);
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

    public static bool SubscribeAddEvent(string id, Action<(uint, ItemFlags, ulong, uint)> action)
    {
        if (Subscribed.ContainsKey(id))
            return true;

        Subscribed.Add(id, new EventSubscriber(EventType.ItemAdded, action));
        return true;
    }

    public static bool SubscribeRemoveEvent(string id, Action<(uint, ItemFlags, ulong, uint)> action)
    {
        if (Subscribed.ContainsKey(id))
            return true;

        Subscribed.Add(id, new EventSubscriber(EventType.ItemRemoved, action));
        return true;
    }

    public static void TriggerInventoryChanged(List<InventoryChange> _, ItemChanges? changedItems)
    {
        if (changedItems != null)
        {
            foreach (var changedItem in changedItems.NewItems)
            {
                if (changedItem.ItemId != 1)
                    foreach (var subscriber in Subscribed.Values.Where(s => s.Type == EventType.ItemAdded))
                        subscriber.Action((changedItem.ItemId, changedItem.Flags, changedItem.CharacterId, (uint)changedItem.Quantity));
            }

            foreach (var changedItem in changedItems.RemovedItems)
            {
                if (changedItem.ItemId != 1)
                    foreach (var subscriber in Subscribed.Values.Where(s => s.Type == EventType.ItemRemoved))
                        subscriber.Action((changedItem.ItemId, changedItem.Flags, changedItem.CharacterId, (uint)changedItem.Quantity));
            }
        }
    }

    public static void Dispose()
    {
        Plugin.InventoryScanner.BagsChanged -= BagsChangedTrigger;
    }

    public enum EventType
    {
        ItemAdded = 0,
        ItemRemoved = 1
    }
}
