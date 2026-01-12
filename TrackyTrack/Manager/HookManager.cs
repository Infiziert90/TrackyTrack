using System.Runtime.InteropServices;
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
    private delegate void ActorControlSelfDelegate(uint category, uint eventId, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, uint param7, uint param8, ulong targetId, byte param9);
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

    private const string UpdateNumberSig = "40 53 48 83 EC ?? 48 63 C2 48 8B D9 44 88 44";
    private delegate void UpdateNumberDelegate(nint agentLotteryDaily, int index, byte value);
    private Hook<UpdateNumberDelegate> UpdateNumberHook;

    private const string UpdatePayoutSig = "E8 ?? ?? ?? ?? 4C 8B 74 24 ?? 89 BB";
    private delegate void UpdatePayoutDelegate(nint agentLotteryDaily, int sum, int mgp);
    private Hook<UpdatePayoutDelegate> UpdatePayoutHook;

    private const string HandleSpawnNPCPacketSig = "E8 ?? ?? ?? ?? B0 ?? 48 8B 5C 24 ?? 48 8B 74 24 ?? 48 83 C4 ?? 5F C3 2D";
    private delegate nint HandleSpawnNPCPacketDelegate(uint a1, nint a2);
    private readonly Hook<HandleSpawnNPCPacketDelegate> HandleSpawnNPCPacketHook;

    private const string HandleSpawnBossPacketSig = "48 89 5C 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B DA 8B F9 E8 ?? ?? ?? ?? 3C ?? 75 ?? E8 ?? ?? ?? ?? 3C ?? 75 ?? 80 BB ?? ?? ?? ?? ?? 75 ?? 8B 05 ?? ?? ?? ?? 39 43 ?? 0F 85 ?? ?? ?? ?? 0F B6 53 ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B6 53 ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 44 24 ?? C7 44 24 ?? ?? ?? ?? ?? BA ?? ?? ?? ?? 66 90 48 8D 80 ?? ?? ?? ?? ?? ?? ?? 0F 10 4B ?? 48 8D 9B ?? ?? ?? ?? 0F 11 40 ?? 0F 10 43 ?? 0F 11 48 ?? 0F 10 4B ?? 0F 11 40 ?? 0F 10 43 ?? 0F 11 48 ?? 0F 10 4B ?? 0F 11 40 ?? 0F 10 43 ?? 0F 11 48 ?? 0F 10 4B ?? 0F 11 40 ?? 0F 11 48 ?? 48 83 EA ?? 75 ?? 4C 8D 44 24";
    private delegate nint HandleSpawnBossPacketDelegate(uint a1, nint a2);
    private readonly Hook<HandleSpawnBossPacketDelegate> HandleSpawnBossPacketHook;

    public uint LastSeenItemId;
    private MiniCactpotData? LastDataSet = null;

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

        var updateNumberPtr = Plugin.SigScanner.ScanText(UpdateNumberSig);
        UpdateNumberHook = Plugin.Hook.HookFromAddress<UpdateNumberDelegate>(updateNumberPtr, UpdateNumberDetour);
        UpdateNumberHook.Enable();

        var updatePayoutPtr = Plugin.SigScanner.ScanText(UpdatePayoutSig);
        UpdatePayoutHook = Plugin.Hook.HookFromAddress<UpdatePayoutDelegate>(updatePayoutPtr, UpdatePayoutDetour);
        UpdatePayoutHook.Enable();

        var handleSpawnNPCPacketPtr = Plugin.SigScanner.ScanText(HandleSpawnNPCPacketSig);
        HandleSpawnNPCPacketHook = Plugin.Hook.HookFromAddress<HandleSpawnNPCPacketDelegate>(handleSpawnNPCPacketPtr, HandleSpawnNPCPacketDetour);
        HandleSpawnNPCPacketHook.Enable();

        var handleSpawnBossPacketPtr = Plugin.SigScanner.ScanText(HandleSpawnBossPacketSig);
        HandleSpawnBossPacketHook = Plugin.Hook.HookFromAddress<HandleSpawnBossPacketDelegate>(handleSpawnBossPacketPtr, HandleSpawnBossPacketDetour);
        HandleSpawnBossPacketHook.Enable();
    }

    public void Dispose()
    {
        DesynthResultHook.Dispose();
        ActorControlSelfHook.Dispose();
        OpenInspectHook.Dispose();
        LootAddedHook.Dispose();
        RetainerTaskHook.Dispose();
        TreasureInteractHook.Dispose();
        UpdateNumberHook.Dispose();
        UpdatePayoutHook.Dispose();
        HandleSpawnNPCPacketHook.Dispose();
        HandleSpawnBossPacketHook.Dispose();
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

    private void ActorControlSelf(uint category, uint eventId, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, uint param7, uint param8, ulong targetId, byte param9)
    {
        ActorControlSelfHook.Original(category, eventId, param1, param2, param3, param4, param5, param6, param7, param8, targetId, param9);

        // Handler for teleport, repair and other message logs
        if (eventId != 517)
            return;

        try
        {
            switch (param1)
            {
                // Teleport log handler
                case 4590:
                    Plugin.TeleportCostHandler(param2);
                    break;
                // Aetheryte ticket log handler
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
                dutyLoot = new Export.DutyLoot(chestObject.Position, chestObject.BaseId, chestObjectId, lowestContentId);

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

            var pos = (Vector3)treasureObj->Position;
            foreach (var (otherPos, _) in OccultUtil.TreasurePositions)
            {
                var dis = Vector3.Distance(otherPos, pos);
                if (dis != 0.0 && dis < 10.0)
                {
                    Plugin.Log.Error($"Found invalid treasure position. ({pos.X}, {pos.Y}, {pos.Z})");
                    Plugin.ChatGui.PrintError("Found invalid treasure position, this is a problem, please contact @infi on discord or per github issues to resolve this.");

                    return;
                }
            }

            Plugin.TimerManager.StartTreasure(baseId, pos);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Unable to track treasure interaction.");
        }
    }

    private void UpdateNumberDetour(nint agentLotteryDaily, int index, byte value)
    {
        UpdateNumberHook.Original(agentLotteryDaily, index, value);
        try
        {
            if (LastDataSet != null)
                return;

            LastDataSet = new MiniCactpotData { Start = { [0] = (byte)index, [1] = value } };
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error while processing UpdateNumber.");
        }
    }

    private void UpdatePayoutDetour(nint agentLotteryDaily, int sum, int mgp)
    {
        UpdatePayoutHook.Original(agentLotteryDaily, sum, mgp);

        try
        {
            if (LastDataSet == null)
            {
                Plugin.Log.Error("Reached UpdatePayout without creating LastDataSet?");
                return;
            }

            var numbers = new Span<byte>((void*)(agentLotteryDaily + 0x38), 9);
            for (var i = 0; i < numbers.Length; i++)
                LastDataSet.FullBoard[i] = numbers[i];

            LastDataSet.Sum = sum;
            LastDataSet.Payout = mgp;

            var character = Plugin.CharacterStorage.GetOrCreate(Plugin.PlayerState.ContentId);
            character.MiniCactpot.Recorded += 1;
            character.MiniCactpot.History.Add(DateTime.Now, LastDataSet);
            Plugin.ConfigurationBase.SaveCharacterConfig();

            Plugin.UploadEntry(new Export.MiniCactpotSet(LastDataSet));
            LastDataSet = null;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error while processing UpdatePayout.");
        }
    }

    private nint HandleSpawnNPCPacketDetour(uint a1, nint a2)
    {
        try
        {
            var packet = Marshal.PtrToStructure<SpawnPacketLayout>(a2);
            if (!Sheets.DisallowedBnpcNames.Contains(packet.BNPCNameId) && !Sheets.DisallowedBnpcBase.Contains(packet.BNPCBaseId))
            {
                var bnpcPairData = new Export.BnpcPair(packet, 1);
                if (Bnpc.UploadHashes.Add(bnpcPairData.Hashed))
                    Plugin.UploadEntry(bnpcPairData);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error while processing HandleSpawnNPC.");
        }

        return HandleSpawnNPCPacketHook.Original(a1, a2);
    }

    private nint HandleSpawnBossPacketDetour(uint a1, nint a2)
    {
        try
        {
            var packet = Marshal.PtrToStructure<SpawnPacketLayout>(a2);
            if (!Sheets.DisallowedBnpcNames.Contains(packet.BNPCNameId) && !Sheets.DisallowedBnpcBase.Contains(packet.BNPCBaseId))
            {
                var bnpcPairData = new Export.BnpcPair(packet, 2);
                if (Bnpc.UploadHashes.Add(bnpcPairData.Hashed))
                    Plugin.UploadEntry(bnpcPairData);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error while processing HandleSpawnBoss.");
        }

        return HandleSpawnBossPacketHook.Original(a1, a2);
    }
}
