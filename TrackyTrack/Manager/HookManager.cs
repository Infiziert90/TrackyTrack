using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using TrackyTrack.Data;

namespace TrackyTrack.Manager;

public unsafe class HookManager
{
    private readonly Plugin Plugin;

    private const string DesynthResultSig = "E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 44 0F B6 43 ?? 4C 8D 4B 17";
    private delegate void DesynthResultDelegate(uint param1, ushort param2, sbyte param3, nint param4, char param5);
    private Hook<DesynthResultDelegate> DesynthResultHook;

    private const string ActorControlSig = "E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64";
    private delegate void ActorControlSelfDelegate(uint category, uint eventId, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, UInt64 targetId, byte param7);
    private Hook<ActorControlSelfDelegate> ActorControlSelfHook;

    private const string OpenInspectSig = "40 53 56 41 56 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B 01";
    private delegate void OpenInspectThingy(nint inspectAgent, int something, InventoryItem* item);
    private Hook<OpenInspectThingy> OpenInspectHook;

    public uint LastSeenItemId;

    public HookManager(Plugin plugin)
    {
        Plugin = plugin;

        var desynthResultPtr = Plugin.SigScanner.ScanText(DesynthResultSig);
        DesynthResultHook = Plugin.Hook.HookFromAddress<DesynthResultDelegate>(desynthResultPtr, DesynthResultPacket);
        DesynthResultHook.Enable();

        var actorControlSelfPtr = Plugin.SigScanner.ScanText(ActorControlSig);
        ActorControlSelfHook = Plugin.Hook.HookFromAddress<ActorControlSelfDelegate>(actorControlSelfPtr, ActorControlSelf);
        ActorControlSelfHook.Enable();

        var openInspectPtr = Plugin.SigScanner.ScanText(OpenInspectSig);
        OpenInspectHook = Plugin.Hook.HookFromAddress<OpenInspectThingy>(openInspectPtr, OpenInspect);
        OpenInspectHook.Enable();
    }

    public void Dispose()
    {
        DesynthResultHook.Dispose();
        ActorControlSelfHook.Dispose();
        OpenInspectHook.Dispose();
    }

    private void OpenInspect(nint inspectAgent, int something, InventoryItem* item)
    {
        OpenInspectHook.Original(inspectAgent, something, item);

        try
        {
            // ItemInspection is called for multiple different use cases, so we ignore all that aren't fragment based
            if (LastSeenItemId == uint.MaxValue)
                return;

            var lostAction = item->ItemId;
            if (lostAction is < 30900 or > 33795)
            {
                Plugin.Log.Warning($"{lostAction} exceeds the allowed item range");
                return;
            }

            Plugin.LockboxHandler(LastSeenItemId, lostAction, 1);
            LastSeenItemId = uint.MaxValue;
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "OpenInspection failed");
        }
    }

    private void DesynthResultPacket(uint param1, ushort param2, sbyte param3, nint param4, char param5)
    {
        DesynthResultHook.Original(param1, param2, param3, param4, param5);

        // DesynthResult is triggered by multiple events
        if (param1 != 3735552)
            return;

        try
        {
            if (Plugin.Configuration.EnableBulkSupport)
                Plugin.BulkHandler();

            if (Plugin.Configuration.EnableDesynthesis)
                Plugin.DesynthHandler();
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Error while parsing desynth result packet");
        }
    }

    private void ActorControlSelf(uint category, uint eventId, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, ulong targetId, byte param7)
    {
        ActorControlSelfHook.Original(category, eventId, param1, param2, param3, param4, param5, param6, targetId, param7);

        // handler for gathering traits
        if (eventId == 309)
        {
            if (Plugin.ClientState.LocalPlayer is { } player && player.EntityId == param2)
            {
                Plugin.TimerManager.Revisited++;
                Plugin.Log.Information($"param1 {param1} param2 {param2:X}");
                Plugin.Log.Information($"Revisit triggered? Node was {Plugin.FrameworkManager.Type}");
            }
        }

        // handler for teleport, repair and other message logs
        if (eventId != 517)
            return;

        try
        {
            switch (param1)
            {
                // teleport log handler
                case 4590:
                    Plugin.TeleportCostHandler(param2);
                    break;
                // aetheryte ticket log handler
                case 4591:
                    Plugin.AetheryteTicketHandler();
                    break;
                // Repair log handler
                case 1388:
                    Plugin.RepairHandler(param2);
                    break;
                // Lockbox handler
                case 1948:
                case 3980:
                    // Sort out the overflow from fragments
                    if (!Lockboxes.Fragments.Contains(param2))
                        Plugin.LockboxHandler(param2, param4, param5);
                    break;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Error while parsing actor control packet");
        }
    }
}
