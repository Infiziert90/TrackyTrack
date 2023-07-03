using Dalamud.Logging;
using Dalamud.Plugin.Ipc;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace TrackyTrack.IPC;

public class AllaganToolsConsumer
{
    private bool Available;
    private long TimeSinceLastCheck;

    public bool Subscribed = false;

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
                Subscribed = false;
                Available = false;
            }

            return Available;
        }
    }

    private ICallGateSubscriber<bool> IsInitialized = null!;
    public ICallGateSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool> ItemAddedEvent = null!;

    private void Subscribe()
    {
        try
        {
            IsInitialized = Plugin.PluginInterface.GetIpcSubscriber<bool>("AllaganTools.IsInitialized");
            ItemAddedEvent = Plugin.PluginInterface.GetIpcSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool>("AllaganTools.ItemAdded");
        }
        catch (Exception e)
        {
            PluginLog.LogDebug($"Failed to subscribe to AllaganTools\nReason: {e}");
        }
    }

    public bool SubscribeToEvent(Action<(uint, InventoryItem.ItemFlags, ulong, uint)> action)
    {
        if (!IsAvailable)
            return false;

        if (Subscribed)
            return true;

        Subscribed = true;
        ItemAddedEvent.Subscribe(action);

        return true;
    }

    public void Unsubscribe(Action<(uint, InventoryItem.ItemFlags, ulong, uint)> action)
    {
        if (!Subscribed)
            return;

        if (!IsAvailable)
            return;

        ItemAddedEvent.Unsubscribe(action);
    }
}
