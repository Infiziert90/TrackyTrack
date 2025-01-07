using Dalamud.Hooking;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using TrackyTrack.Data;

namespace TrackyTrack.Manager;

public unsafe class HookManager
{
    private readonly Plugin Plugin;

    private const string DesynthResultSig = "E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 44 0F B6 43 ?? 4C 8D 4B 17";
    private delegate void DesynthResultDelegate(uint param1, ushort param2, sbyte param3, nint param4, char param5);
    private Hook<DesynthResultDelegate> DesynthResultHook;

    private const string ActorControlSig = "E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64";
    private delegate void ActorControlSelfDelegate(uint category, uint eventId, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, ulong targetId, byte param7);
    private Hook<ActorControlSelfDelegate> ActorControlSelfHook;

    private const string OpenInspectSig = "40 53 56 41 54 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B 01";
    private delegate void OpenInspectThingy(nint inspectAgent, int something, InventoryItem* item);
    private Hook<OpenInspectThingy> OpenInspectHook;

    private const string LootAddedSig = "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 44 89 4C 24";
    private delegate byte LootAddedDelegate(Loot* a1, uint chestObjectId, uint chestItemIndex, uint itemId, ushort itemCount, nint materia, nint glamourStainIds, uint glamourItemId, RollState rollState, RollResult rollResult, float time, float maxTime, byte rollValue, byte a14, LootMode lootMode, int a16);
    private Hook<LootAddedDelegate> LootAddedHook;

    private const string RetainerTaskResultSig = "E8 ?? ?? ?? ?? 48 89 9B ?? ?? ?? ?? 48 8B CF 48 8B 17 FF 52 40 89 83 ?? ?? ?? ?? 33 D2 48 8D 4D A0";
    private delegate void RetainerTaskResultDelegate(AgentRetainerTask* agent, nint someLuaPointer, nint packet);
    private Hook<RetainerTaskResultDelegate> RetainerTaskHook;

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

        var lootAddedPtr = Plugin.SigScanner.ScanText(LootAddedSig);
        LootAddedHook = Plugin.Hook.HookFromAddress<LootAddedDelegate>(lootAddedPtr, LootAddedDetour);
        LootAddedHook.Enable();

        var retainerTaskPtr = Plugin.SigScanner.ScanText(RetainerTaskResultSig);
        RetainerTaskHook = Plugin.Hook.HookFromAddress<RetainerTaskResultDelegate>(retainerTaskPtr, RetainerTaskDetour);
        RetainerTaskHook.Enable();
    }

    public void Dispose()
    {
        DesynthResultHook.Dispose();
        ActorControlSelfHook.Dispose();
        OpenInspectHook.Dispose();
        LootAddedHook.Dispose();
        RetainerTaskHook.Dispose();
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


    private byte LootAddedDetour(Loot* a1, uint chestObjectId, uint chestItemIndex, uint itemId, ushort itemCount, nint materia, nint glamourStainIds, uint glamourItemId, RollState rollState, RollResult rollResult, float time, float maxTime, byte rollValue, byte a14, LootMode lootMode, int a16)
    {
        var r = LootAddedHook.Original(a1, chestObjectId, chestItemIndex, itemId, itemCount, materia, glamourStainIds, glamourItemId, rollState, rollResult, time, maxTime, rollValue, a14, lootMode, a16);

        #if RELEASE
        return r;
        #endif

        try
        {
            Plugin.Log.Information($"Loot added: {r} ID1 {GroupManager.Instance()->MainGroup.PartyId} ID2 {GroupManager.Instance()->MainGroup.PartyId_2}");
            Plugin.Log.Information($"chestObjectId {chestObjectId:X} itemId {itemId} itemCount {itemCount} roll {rollValue} a14 {a14} a16 {a16}");

            var chestObject = Plugin.ObjectTable.SearchByEntityId(chestObjectId);
            if (chestObject != null && chestObject.IsValid())
            {
                Plugin.Log.Information($"Chest Info: {chestObject.Name} BaseId {chestObject.DataId} Field7 {Sheets.TreasureSheet.GetRow(chestObject.DataId).Unknown7}");
                Plugin.Log.Information($"Chest Position: {chestObject.Position.X} {chestObject.Position.Y} {chestObject.Position.Z}");

                var map = Sheets.MapSheet.GetRow(Plugin.ClientState.MapId)!;
                var transient = Sheets.TerritoryTransientSheet.GetRow(Plugin.ClientState.TerritoryType)!;

                var mapPos = MapUtil.WorldToMap(chestObject.Position, map, transient);
                Plugin.Log.Information($"Mao Info: {map.PlaceName.Value!.Name} | {map.PlaceNameRegion.Value!.Name} | {map.PlaceNameSub.Value!.Name}");
                Plugin.Log.Information($"Map Position: {mapPos.X} {mapPos.Y} {mapPos.Z}");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Error while parsing actor control packet");
        }

        return r;
    }

    private void RetainerTaskDetour(AgentRetainerTask* agent, nint someLuaPointer, nint packet)
    {
        RetainerTaskHook.Original(agent, someLuaPointer, packet);

        var retainer = RetainerManager.Instance();
        var venture = AgentRetainerTask.Instance();
        if (venture == null || retainer == null)
            return;

        try
        {
            var activeRetainer = retainer->GetActiveRetainer();
            if (activeRetainer == null)
                return;

            if (venture->RetainerTaskId == 0)
            {
                Plugin.Log.Warning("RetainerTaskId was 0?");
                return;
            }

            var primary = venture->RewardItemIds[0];
            var primaryHQ = primary > 1_000_000;
            if (primaryHQ)
                primary -= 1_000_000;
            var primaryCount = (short) venture->RewardItemCount[0];

            var additionalItem = venture->RewardItemIds[1];
            var additionalHQ = additionalItem > 1_000_000;
            if (additionalHQ)
                additionalItem -= 1_000_000;
            var additionalCount = (short) venture->RewardItemCount[1];

            Plugin.RetainerHandler(venture->RetainerTaskId, activeRetainer->Level, new VentureItem(primary, primaryCount, primaryHQ), new VentureItem(additionalItem, additionalCount, additionalHQ));
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e, "Unable to track retainer result.");
        }
    }
}
