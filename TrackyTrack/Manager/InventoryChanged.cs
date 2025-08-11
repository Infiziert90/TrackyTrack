using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;

namespace TrackyTrack.Manager;

public class InventoryChanged
{
    public event ItemAddedEvent? OnItemAdded;
    public delegate void ItemAddedEvent((uint ItemId, uint Quantity) changedItem);

    public event ItemRemovedEvent? OnItemRemoved;
    public delegate void ItemRemovedEvent((uint ItemId, uint Quantity) changedItem);

    public event ItemsChangedEvent? OnItemsChanged;
    public delegate void ItemsChangedEvent((uint ItemId, int Quantity)[] changedItems);

    private const int Delay = 300; // 300ms
    private long CurrentTickDelay;
    private readonly List<(uint ItemId, int Quantity)> DelayedChanges = [];

    public event DelayedItemsChangedEvent? OnDelayedItemsChanged;
    public delegate void DelayedItemsChangedEvent((uint ItemId, int Quantity)[] changedItems);

    public InventoryChanged()
    {
        Plugin.GameInventory.InventoryChangedRaw += TriggerInventoryChanged;
        Plugin.Framework.Update += ProcessFrameDelayedLoot;
    }

    public void Dispose()
    {
        Plugin.GameInventory.InventoryChangedRaw -= TriggerInventoryChanged;
        Plugin.Framework.Update -= ProcessFrameDelayedLoot;
    }

    private void TriggerInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
    {
        var changes = new Dictionary<uint, (int NewQuantity, int OldQuantity)>();
        foreach (var (e, _, type) in events.Select(e => (e, e.Item, e.Type)))
        {
            if (e.Item.ContainerType == GameInventoryType.DamagedGear)
                continue;

            switch (type)
            {
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

        if (changes.Count != 0)
        {
            var processedChanges = changes.Select(pair => (pair.Key, pair.Value.NewQuantity - pair.Value.OldQuantity)).ToArray();

            foreach (var processedChange in processedChanges)
            {
                if (!Sheets.ItemSheet.TryGetRow(processedChange.Key, out var itemRow))
                    continue;

                // Check if the changed item is the soul crystal and return
                if (itemRow.ItemUICategory.RowId == 62)
                    return;
            }

            // Check if there isn't a frame delay running
            // Otherwise add the current loot changes to the list
            if (CurrentTickDelay == 0)
                CurrentTickDelay = Environment.TickCount64;

            DelayedChanges.AddRange(processedChanges);

            // Coffer checks added and removed
            OnItemsChanged?.Invoke(processedChanges);

            foreach (var (itemId, changedQuantity) in processedChanges)
            {
                if (itemId == 1)
                    continue;

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

    private void ProcessFrameDelayedLoot(IFramework _)
    {
        // Early return if no delay is required at this time
        if (CurrentTickDelay == 0)
            return;

        if (Environment.TickCount64 < CurrentTickDelay + Delay)
            return;

        CurrentTickDelay = 0;
        if (DelayedChanges.Count == 0)
            return;

        OnDelayedItemsChanged?.Invoke(DelayedChanges.ToArray());
        DelayedChanges.Clear();
    }
}
