using Dalamud.Hooking;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using TrackyTrack.Data;

namespace TrackyTrack.Manager;

public unsafe class HookManager
{
    private readonly Plugin Plugin;

    private const string DesynthResultSig = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 49 8B D9 41 0F B6 F8 0F B7 F2 8B E9 E8 ?? ?? ?? ?? 44 0F B6 54 24 ?? 44 0F B6 CF 44 88 54 24 ?? 44 0F B7 C6 8B D5";
    private delegate void DesynthResultDelegate(nuint a1, ushort eventId, byte responseId, uint* args, byte argCount);
    private Hook<DesynthResultDelegate> DesynthResultHook;

    private const string ActorControlSig = "E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64";
    private delegate void ActorControlSelfDelegate(uint category, uint eventId, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, ulong targetId, byte param7);
    private Hook<ActorControlSelfDelegate> ActorControlSelfHook;

    private const string OpenInspectSig = "40 53 56 41 54 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B 01";
    private delegate void OpenInspectThingy(nint inspectAgent, int starRating, InventoryItem* reward);
    private Hook<OpenInspectThingy> OpenInspectHook;

    private const string LootAddedSig = "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 44 89 4C 24";
    private delegate byte LootAddedDelegate(Loot* a1, uint chestObjectId, uint chestItemIndex, uint itemId, ushort itemCount, nint materia, nint glamourStainIds, uint glamourItemId, RollState rollState, RollResult rollResult, float time, float maxTime, byte rollValue, byte a14, LootMode lootMode, int a16, uint a17);
    private Hook<LootAddedDelegate> LootAddedHook;

    private const string RetainerTaskResultSig = "E8 ?? ?? ?? ?? 48 89 9B ?? ?? ?? ?? 48 8B CF 48 8B 17 FF 52 48 89 83 ?? ?? ?? ?? 33 D2 48 8D 4D A0";
    private delegate void RetainerTaskResultDelegate(AgentRetainerTask* agent, nint someLuaPointer, nint packet);
    private Hook<RetainerTaskResultDelegate> RetainerTaskHook;

    private const string TreasureInteractSig = "E9 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 85 C0";
    private delegate void TreasureInteractDelegate(Loot* loot, Treasure* treasureObj);
    private Hook<TreasureInteractDelegate> TreasureInteractHook;

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

        var treasureInteractPtr = Plugin.SigScanner.ScanText(TreasureInteractSig);
        TreasureInteractHook = Plugin.Hook.HookFromAddress<TreasureInteractDelegate>(treasureInteractPtr, TreasureInteractDetour);
        TreasureInteractHook.Enable();
    }

    public void Dispose()
    {
        DesynthResultHook.Dispose();
        ActorControlSelfHook.Dispose();
        OpenInspectHook.Dispose();
        LootAddedHook.Dispose();
        RetainerTaskHook.Dispose();
        TreasureInteractHook.Dispose();
    }

    private void OpenInspect(nint inspectAgent, int starRating, InventoryItem* reward)
    {
        OpenInspectHook.Original(inspectAgent, starRating, reward);

        try
        {
            // ItemInspection is called for multiple different use cases, so we ignore all that aren't fragment based
            if (LastSeenItemId == uint.MaxValue)
                return;

            var lostAction = reward->ItemId;
            if (lostAction is < 30900 or > 33795)
            {
                Plugin.Log.Warning($"{lostAction} exceeds the allowed item range");
                return;
            }

            Plugin.LockboxHandler(LastSeenItemId, lostAction, 1);
            LastSeenItemId = uint.MaxValue;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "OpenInspection failed");
        }
    }

    private void DesynthResultPacket(nuint a1, ushort eventId, byte responseId, uint* args, byte argCount)
    {
        DesynthResultHook.Original(a1, eventId, responseId, args, argCount);

        // DesynthResult is triggered by multiple events
        if (a1 != 3735552)
            return;

        try
        {
            if (Plugin.Configuration.EnableBulkSupport)
                Plugin.BulkHandler();

            if (Plugin.Configuration.EnableDesynthesis)
                Plugin.DesynthHandler();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error while parsing desynth result packet");
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
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error while parsing actor control packet");
        }
    }

    private byte LootAddedDetour(Loot* a1, uint chestObjectId, uint chestItemIndex, uint itemId, ushort itemCount, nint materia, nint glamourStainIds, uint glamourItemId, RollState rollState, RollResult rollResult, float time, float maxTime, byte rollValue, byte a14, LootMode lootMode, int a16, uint a17)
    {
        var r = LootAddedHook.Original(a1, chestObjectId, chestItemIndex, itemId, itemCount, materia, glamourStainIds, glamourItemId, rollState, rollResult, time, maxTime, rollValue, a14, lootMode, a16, a17);

        // This hook can be called multiple times for different scenarios, but we only care about the initial one
        if (time < maxTime)
            return r;

        try
        {
            var group = GroupManager.Instance();
            var lowestContentId = ulong.MaxValue;
            foreach (var member in group->MainGroup.PartyMembers)
            {
                if (member.ContentId != 0 && member.ContentId < lowestContentId)
                    lowestContentId = member.ContentId;
            }

            if (group->MainGroup.IsAlliance)
            {
                foreach (var member in group->MainGroup.AllianceMembers)
                {
                    if (member.ContentId != 0 && member.ContentId < lowestContentId)
                        lowestContentId = member.ContentId;
                }
            }

            var chestObject = Plugin.ObjectTable.SearchByEntityId(chestObjectId);
            if (chestObject == null || !chestObject.IsValid())
                return r;

            Plugin.TimerManager.StartLoot();

            if (!Plugin.TimerManager.LootCache.TryGetValue(chestObjectId, out var dutyLoot))
                dutyLoot = new Export.DutyLoot(chestObject.Position, chestObject.DataId, chestObjectId, lowestContentId);

            dutyLoot.AddContent(itemId, itemCount, chestItemIndex);
            Plugin.TimerManager.LootCache[chestObjectId] = dutyLoot;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error while parsing loot result.");
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

            var primary = ItemUtil.GetBaseId(venture->RewardItemIds[0]);
            var primaryCount = (short) venture->RewardItemCount[0];

            var additionalItem = ItemUtil.GetBaseId(venture->RewardItemIds[1]);
            var additionalCount = (short) venture->RewardItemCount[1];

            Plugin.RetainerHandler(venture->RetainerTaskId, activeRetainer->Level, new VentureItem(primary.ItemId, primaryCount, primary.Kind == ItemKind.Hq), new VentureItem(additionalItem.ItemId, additionalCount, additionalItem.Kind == ItemKind.Hq));
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Unable to track retainer result.");
        }
    }

    private void TreasureInteractDetour(Loot* loot, Treasure* treasureObj)
    {
        TreasureInteractHook.Original(loot, treasureObj);

        try
        {
            if (treasureObj == null || loot == null)
                return;

            // This range should include all random coffer
            var baseId = treasureObj->BaseId;
            if (baseId is > 1856 or < 1789)
                return;

            Plugin.TimerManager.StartTreasure(baseId, treasureObj->Position);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Unable to track treasure interaction.");
        }
    }
}
