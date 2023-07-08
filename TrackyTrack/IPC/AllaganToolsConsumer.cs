using Dalamud.Logging;
using Dalamud.Plugin.Ipc;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace TrackyTrack.IPC;

public class AllaganToolsConsumer
{
    private bool Available;
    private long TimeSinceLastCheck;

    public record EventSubscriber(int Type, Action<(uint, InventoryItem.ItemFlags, ulong, uint)> Action);
    public readonly Dictionary<string, EventSubscriber> Subscribed = new();

    public AllaganToolsConsumer() => Subscribe();

    public bool IsAvailable
    {
        get
        {
            if (TimeSinceLastCheck + 5000 > Environment.TickCount64)
            {
                return Available;
            }

            try
            {
                TimeSinceLastCheck = Environment.TickCount64;

                IsInitialized.InvokeFunc();
                Available = true;
            }
            catch
            {
                // Lost IPC, so we can clear
                Subscribed.Clear();
                Available = false;
            }

            return Available;
        }
    }

    private ICallGateSubscriber<bool> IsInitialized = null!;
    public ICallGateSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool> ItemAddedEvent = null!;
    public ICallGateSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool> ItemRemoveEvent = null!;

    private void Subscribe()
    {
        try
        {
            IsInitialized = Plugin.PluginInterface.GetIpcSubscriber<bool>("AllaganTools.IsInitialized");
            ItemAddedEvent = Plugin.PluginInterface.GetIpcSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool>("AllaganTools.ItemAdded");
            ItemRemoveEvent = Plugin.PluginInterface.GetIpcSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool>("AllaganTools.ItemRemoved");
        }
        catch (Exception e)
        {
            PluginLog.LogDebug($"Failed to subscribe to AllaganTools\nReason: {e}");
        }
    }

    public bool SubscribeAddEvent(string id, Action<(uint, InventoryItem.ItemFlags, ulong, uint)> action)
    {
        if (!IsAvailable)
            return false;

        if (Subscribed.ContainsKey(id))
            return true;

        Subscribed.Add(id, new EventSubscriber(0, action));
        ItemAddedEvent.Subscribe(action);

        return true;
    }

    public bool SubscribeRemoveEvent(string id, Action<(uint, InventoryItem.ItemFlags, ulong, uint)> action)
    {
        if (!IsAvailable)
            return false;

        if (Subscribed.ContainsKey(id))
            return true;

        Subscribed.Add(id, new EventSubscriber(1, action));
        ItemRemoveEvent.Subscribe(action);

        return true;
    }

    public void Dispose()
    {
        if (!IsAvailable)
            return;

        if (!Subscribed.Any())
            return;

        foreach (var subscribedEvent in Subscribed.Values)
        {
            switch (subscribedEvent.Type)
            {
                case 0:
                    ItemAddedEvent.Unsubscribe(subscribedEvent.Action);
                    break;
                case 1:
                    ItemRemoveEvent.Unsubscribe(subscribedEvent.Action);
                    break;
            }
        }
    }
}
