using Dalamud.Hooking;

namespace TrackyTrack.Manager;

public class HookManager
{
    private Plugin Plugin;

    private const string DesynthResultSig = "E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 44 0F B6 43 ?? 4C 8D 4B 17";
    private delegate void DesynthResultDelegate(uint param1, ushort param2, sbyte param3, Int64 param4, char param5);
    private Hook<DesynthResultDelegate> DesynthResultHook;

    private const string ActorControlSig = "E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64";
    private delegate void ActorControlSelfDelegate(uint category, uint eventId, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, UInt64 targetId, byte param7);
    private Hook<ActorControlSelfDelegate> ActorControlSelfHook;

    public HookManager(Plugin plugin)
    {
        Plugin = plugin;

        var desynthResultPtr = Plugin.SigScanner.ScanText(DesynthResultSig);
        DesynthResultHook = Plugin.Hook.HookFromAddress<DesynthResultDelegate>(desynthResultPtr, DesynthResultPacket);
        DesynthResultHook.Enable();

        var actorControlSelfPtr = Plugin.SigScanner.ScanText(ActorControlSig);
        ActorControlSelfHook = Plugin.Hook.HookFromAddress<ActorControlSelfDelegate>(actorControlSelfPtr, ActorControlSelf);
        ActorControlSelfHook.Enable();
    }

    public void Dispose()
    {
        DesynthResultHook.Dispose();
        ActorControlSelfHook.Dispose();
    }

    private void DesynthResultPacket(uint param1, ushort param2, sbyte param3, Int64 param4, char param5)
    {
        DesynthResultHook.Original(param1, param2, param3, param4, param5);

        // DesynthResult is triggered by multiple events
        if (param1 != 3735552)
        {
            Plugin.Log.Debug("Received param1 that isn't DesynthResult");
            Plugin.Log.Debug($"Param1 {param1}");
            return;
        }

        try
        {
            if (Plugin.Configuration.EnableBulkSupport)
                Plugin.BulkHandler();

            if (Plugin.Configuration.EnableDesynthesis)
                Plugin.DesynthHandler();
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e.Message);
            Plugin.Log.Warning(e.StackTrace ?? "Unknown");
        }
    }

    private void ActorControlSelf(uint category, uint eventId, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, UInt64 targetId, byte param7)
    {
        ActorControlSelfHook.Original(category, eventId, param1, param2, param3, param4, param5, param6, targetId, param7);

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
                    Plugin.LockboxHandler(param2, param4, param5);
                    break;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e.Message);
            Plugin.Log.Warning(e.StackTrace ?? "Unknown");
        }

        Plugin.Log.Debug($"Cate {category} id {eventId} param1 {param1} param2 {param2} param3 {param3} param4 {param4} param5 {param5} param6 {param6} target {targetId} param7 {param7}");
    }
}
