using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;

namespace TrackyTrack.Manager;

public class InventoryChanged
{
    public event ItemAddedEvent? OnItemAdded;
    public delegate void ItemAddedEvent((uint ItemId, uint Quantity) changedItem);

    public event ItemRemovedEvent? OnItemRemoved;
    public delegate void ItemRemovedEvent((uint ItemId, uint Quantity) changedItem);

    public event ItemsChangedEvent? OnItemsChanged;
    public delegate void ItemsChangedEvent((uint ItemId, long Quantity)[] changedItems);

    public InventoryChanged()
    {
        Plugin.GameInventory.InventoryChangedRaw += TriggerInventoryChanged;
    }

    public void TriggerInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
    {
        var changes = new Dictionary<uint, (uint NewQuantity, uint OldQuantity)>();
        foreach (var (e, _, type) in events.Select(e => (e, e.Item, e.Type)))
        {
            if (e.Item.ContainerType == GameInventoryType.DamagedGear)
                continue;

            switch (type) {
                case GameInventoryEvent.Added when e is InventoryItemAddedArgs { Item: var item }:
                    if (!changes.TryAdd(item.ItemId, (item.Quantity, 0)))
                        changes[item.ItemId] = (changes[item.ItemId].NewQuantity + item.Quantity, changes[item.ItemId].OldQuantity);
                    break;

                case GameInventoryEvent.Removed when e is InventoryItemRemovedArgs { Item: var item }:
                    if (!changes.TryAdd(item.ItemId, (0, item.Quantity)))
                        changes[item.ItemId] = (changes[item.ItemId].NewQuantity, changes[item.ItemId].OldQuantity + item.Quantity);
                    break;

                case GameInventoryEvent.Changed when e is InventoryItemChangedArgs { OldItemState: var oldItem, Item: var newItem }:
                    changes.TryAdd(newItem.ItemId, (0, 0));
                    changes.TryAdd(oldItem.ItemId, (0, 0));


                    if (oldItem.ItemId == newItem.ItemId)
                    {
                        changes[newItem.ItemId] = (changes[newItem.ItemId].OldQuantity + newItem.Quantity, changes[newItem.ItemId].OldQuantity + oldItem.Quantity);
                    }
                    else
                    {
                        // New added item
                        changes[newItem.ItemId] = (changes[newItem.ItemId].NewQuantity + newItem.Quantity, changes[newItem.ItemId].OldQuantity);

                        // Old removed item
                        changes[oldItem.ItemId] = (changes[oldItem.ItemId].NewQuantity, changes[oldItem.ItemId].OldQuantity + oldItem.Quantity);
                    }
                    break;
            }
        }

        if (changes.Any())
        {
            // Coffer checks added and removed
            OnItemsChanged?.Invoke(changes.Select(pair => (pair.Key, (int) pair.Value.NewQuantity - pair.Value.OldQuantity)).ToArray());

            foreach (var (itemId, (newQuantity, oldQuantity)) in changes)
            {
                if (itemId == 1)
                    continue;

                var changedQuantity = (int) newQuantity - oldQuantity;
                switch (changedQuantity)
                {
                    case > 0:
                        OnItemAdded?.Invoke((itemId, (uint) changedQuantity));
                        break;
                    case < 0:
                        OnItemRemoved?.Invoke((itemId, (uint) (changedQuantity * -1)));
                        break;
                }
            }
        }
    }

    public void Dispose()
    {
        Plugin.GameInventory.InventoryChangedRaw -= TriggerInventoryChanged;
    }
}
