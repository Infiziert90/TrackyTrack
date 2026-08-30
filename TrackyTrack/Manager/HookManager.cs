using Dalamud.Hooking;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TrackyTrack.Data;

namespace TrackyTrack.Manager;

public unsafe class HookManager
{
    private readonly Plugin Plugin;

    private const string LootAddedSig = "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 44 89 4C 24";
    private delegate byte LootAddedDelegate(Loot* a1, uint chestObjectId, uint chestItemIndex, uint itemId, ushort itemCount, nint materia, nint glamourStainIds, uint glamourItemId, RollState rollState, RollResult rollResult, float time, float maxTime, byte rollValue, byte a14, LootMode lootMode, int a16, uint a17);
    private Hook<LootAddedDelegate> LootAddedHook;

    private const string TreasureInteractSig = "E9 ?? ?? ?? ?? 48 63 05";
    private delegate void TreasureInteractDelegate(Loot* loot, Treasure* treasureObj);
    private Hook<TreasureInteractDelegate> TreasureInteractHook;

    private const string FashionToggleResultSig = "48 89 54 24 ?? 48 89 4C 24 ?? 53 55 41 56 48 83 EC 50";
    private delegate char FashionToggleResultWindowDelegate(AgentFashion* agentFashion, AgentFashion.FashionCheckDataStruct* data, nint unk);
    private readonly Hook<FashionToggleResultWindowDelegate> FashionToggleResultWindowHook;

    private Hook<PacketDispatcher.Delegates.HandleActorControlPacket>? HandleActorControlPacketHook { get; init; }
    private Hook<AgentItemInspection.Delegates.OpenResult>? OpenResultHook { get; init; }
    private Hook<AgentRetainerTask.Delegates.OpenRetainerTaskResult>? OpenRetainerTaskResultHook { get; init; }
    private Hook<AgentLotteryDaily.Delegates.UpdateNumber>? UpdateNumberHook { get; init; }
    private Hook<AgentLotteryDaily.Delegates.UpdatePayout>? UpdatePayoutHook { get; init; }
    private Hook<PacketDispatcher.Delegates.HandleSpawnNpcPacket>? HandleSpawnNPCPacketHook { get; init; }
    private Hook<AgentFateReward.Delegates.EnqueueReward>? EnqueueRewardHook { get; init; }
    private Hook<PacketDispatcher.Delegates.OnReceivePacket>? OnReceiveHook { get; init; }
    private Hook<ContentsFinderQueueInfo.Delegates.OnQueuePop>? OnDutyPopHook { get; init; }
    private Hook<PacketDispatcher.Delegates.HandleEventYieldPacket>? HandleEventYieldPacketHook { get; init; }

    public uint LastSeenItemId;
    private MiniCactpotData? LastDataSet;

    public readonly HashSet<string> UploadHashes = [];

    public HookManager(Plugin plugin)
    {
        Plugin = plugin;

        HandleActorControlPacketHook = Plugin.Hook.HookFromAddress<PacketDispatcher.Delegates.HandleActorControlPacket>(PacketDispatcher.MemberFunctionPointers.HandleActorControlPacket, HandleActorControlPacketDetour);
        HandleActorControlPacketHook.Enable();

        OpenResultHook = Plugin.Hook.HookFromAddress<AgentItemInspection.Delegates.OpenResult>(AgentItemInspection.MemberFunctionPointers.OpenResult, OpenResultDetour);
        OpenResultHook.Enable();

        var lootAddedPtr = Plugin.SigScanner.ScanText(LootAddedSig);
        LootAddedHook = Plugin.Hook.HookFromAddress<LootAddedDelegate>(lootAddedPtr, LootAddedDetour);
        LootAddedHook.Enable();

        OpenRetainerTaskResultHook = Plugin.Hook.HookFromAddress<AgentRetainerTask.Delegates.OpenRetainerTaskResult>(AgentRetainerTask.MemberFunctionPointers.OpenRetainerTaskResult, OpenRetainerTaskResultDetour);
        OpenRetainerTaskResultHook.Enable();

        var treasureInteractPtr = Plugin.SigScanner.ScanText(TreasureInteractSig);
        TreasureInteractHook = Plugin.Hook.HookFromAddress<TreasureInteractDelegate>(treasureInteractPtr, TreasureInteractDetour);
        TreasureInteractHook.Enable();

        var fashionToggleResultPtr = Plugin.SigScanner.ScanText(FashionToggleResultSig);
        FashionToggleResultWindowHook = Plugin.Hook.HookFromAddress<FashionToggleResultWindowDelegate>(fashionToggleResultPtr, FashionToggleResultDetour);
        FashionToggleResultWindowHook.Enable();

        EnqueueRewardHook = Plugin.Hook.HookFromAddress<AgentFateReward.Delegates.EnqueueReward>(AgentFateReward.MemberFunctionPointers.EnqueueReward, EnqueueRewardDetour);
        EnqueueRewardHook.Enable();

        UpdateNumberHook = Plugin.Hook.HookFromAddress<AgentLotteryDaily.Delegates.UpdateNumber>(AgentLotteryDaily.MemberFunctionPointers.UpdateNumber, UpdateNumberDetour);
        UpdateNumberHook.Enable();

        UpdatePayoutHook = Plugin.Hook.HookFromAddress<AgentLotteryDaily.Delegates.UpdatePayout>(AgentLotteryDaily.MemberFunctionPointers.UpdatePayout, UpdatePayoutDetour);
        UpdatePayoutHook.Enable();

        HandleSpawnNPCPacketHook = Plugin.Hook.HookFromAddress<PacketDispatcher.Delegates.HandleSpawnNpcPacket>(PacketDispatcher.MemberFunctionPointers.HandleSpawnNpcPacket, HandleSpawnNPCPacketDetour);
        HandleSpawnNPCPacketHook.Enable();

        OnDutyPopHook = Plugin.Hook.HookFromAddress<ContentsFinderQueueInfo.Delegates.OnQueuePop>(ContentsFinderQueueInfo.MemberFunctionPointers.OnQueuePop, OnQueuePopDetour);
        OnDutyPopHook.Enable();

        HandleEventYieldPacketHook = Plugin.Hook.HookFromAddress<PacketDispatcher.Delegates.HandleEventYieldPacket>(PacketDispatcher.MemberFunctionPointers.HandleEventYieldPacket, HandleEventYieldPacketDetour);
        HandleEventYieldPacketHook.Enable();

        // OnReceiveHook = Plugin.Hook.HookFromAddress<PacketDispatcher.Delegates.OnReceivePacket>((nint)PacketDispatcher.StaticVirtualTablePointer->OnReceivePacket, OnReceivePacketDetour);
        // OnReceiveHook.Enable();
    }

    public void Dispose()
    {
        HandleActorControlPacketHook?.Dispose();
        OpenResultHook?.Dispose();
        LootAddedHook.Dispose();
        OpenRetainerTaskResultHook?.Dispose();
        TreasureInteractHook.Dispose();
        FashionToggleResultWindowHook.Dispose();
        EnqueueRewardHook?.Dispose();
        UpdateNumberHook?.Dispose();
        UpdatePayoutHook?.Dispose();
        HandleSpawnNPCPacketHook?.Dispose();
        OnDutyPopHook?.Dispose();
        HandleEventYieldPacketHook?.Dispose();
        // OnReceiveHook?.Dispose();
    }

    private HashSet<ushort> Ignore = [552, 246, 274, 332, 526, 363, 490, 636, 113, 128, 893, 258, 151];
    private void OnReceivePacketDetour(PacketDispatcher* thisPtr, uint targetId, nint packet)
    {
        try
        {
            var opCode = *(ushort*)(packet + 2);
            OnReceiveHook!.Original(thisPtr, targetId, packet);

            if (Ignore.Contains(opCode))
                return;

            Plugin.Log.Information($"Opcode: {opCode}");
            Utils.PrintMemoryArea(packet, 0x200);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error in OnReceivePacketDetour.");
        }
    }


    private void OnQueuePopDetour(ContentsFinderQueueInfo* thisPtr, ContentsFinderQueueState newState, uint contentId, nint a4, bool isInProgressParty, ContentsFinder.LootRule lootRule, ulong inProgressPartyStartTimestamp, nint a8, bool isUnrestrictedParty, bool isMinimalIl, bool isSilenceEcho, bool isExplorerMode, bool isLevelSync, bool isLimitedLeveling)
    {
        try
        {
            OnDutyPopHook!.Original(thisPtr, newState, contentId, a4, isInProgressParty, lootRule, inProgressPartyStartTimestamp, a8, isUnrestrictedParty, isMinimalIl, isSilenceEcho, isExplorerMode, isLevelSync, isLimitedLeveling);

            Plugin.TempManager.StartRoulette(thisPtr->QueuedContentRouletteId, thisPtr->QueuedClassJobId, isInProgressParty, isLimitedLeveling);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Unable to process queue pop.");
        }
    }

    private void EnqueueRewardDetour(AgentFateReward* agent, AgentFateReward.Reward* reward)
    {
        try
        {
            EnqueueRewardHook!.Original(agent, reward);
            Plugin.UploadEntry(new Export.FateReward(reward));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Unable to enqueue reward.");
        }
    }

    private void OpenResultDetour(AgentItemInspection* thisPtr, int starRating, InventoryItem* reward)
    {
        OpenResultHook!.Original(thisPtr, starRating, reward);

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

    private void HandleActorControlPacketDetour(uint entityId, uint category, uint arg1, uint arg2, uint arg3, uint arg4, uint arg5, uint arg6, uint arg7, uint arg8, GameObjectId targetId, bool isRecorded)
    {
        HandleActorControlPacketHook!.Original(entityId, category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, targetId, isRecorded);

        if (isRecorded)
            return;

        if (entityId != Control.Instance()->LocalPlayerEntityId)
            return;

        // Handler for teleport, repair and other message logs
        if (category != 517)
            return;

        try
        {
            switch (arg1)
            {
                // Teleport log handler
                case 4590:
                    Plugin.TeleportCostHandler(arg2);
                    break;
                // Aetheryte ticket log handler
                case 4591:
                    Plugin.AetheryteTicketHandler();
                    break;
                // Repair log handler
                case 1388:
                    Plugin.RepairHandler(arg2);
                    break;
                // Lockbox handler
                case 1948:
                case 3980:
                    // Sort out the overflow from fragments
                    if (!Lockboxes.Fragments.Contains(arg2))
                        Plugin.LockboxHandler(arg2, arg4, arg5);
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

    private void OpenRetainerTaskResultDetour(AgentRetainerTask* agent, AtkModuleInterface.AtkEventInterface* eventInterface, AgentRetainerTask.Data* resultData)
    {
        OpenRetainerTaskResultHook!.Original(agent, eventInterface, resultData);

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

            var primary = ItemUtil.GetBaseId(venture->RetainerData.RewardItemIds[0]);
            var primaryCount = (short) venture->RetainerData.RewardItemCount[0];

            var additionalItem = ItemUtil.GetBaseId(venture->RetainerData.RewardItemIds[1]);
            var additionalCount = (short) venture->RetainerData.RewardItemCount[1];

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
            Plugin.Log.Debug($"Interacting with {treasureObj->BaseId}");
            if ((OccultTerritory)Plugin.ClientState.TerritoryType == OccultTerritory.SouthHorn)
            {
                if (treasureObj->BaseId is > 1856 or < 1789)
                    return;
            }
            else
            {
                if (treasureObj->BaseId is > 2073 or < 2006)
                    return;
            }

            Plugin.TimerManager.StartOccultTreasure(treasureObj->BaseId, treasureObj->Position);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Unable to track treasure interaction.");
        }
    }

    private char FashionToggleResultDetour(AgentFashion* agentFashion, AgentFashion.FashionCheckDataStruct* data, nint unk)
    {
        var res = FashionToggleResultWindowHook.Original(agentFashion, data, unk);

        if (agentFashion != null && agentFashion->OpenType != AgentFashionOpenType.Result)
            return res;

        var result = new FashionReportResult
        {
            WeekNum = agentFashion->FashionCheckData.WeeklyTheme - 9u,
            Score = agentFashion->FashionCheckData.Score
        };

        var hints = agentFashion->FashionCheckData.ItemThemes;
        var stamps = agentFashion->FashionCheckData.ItemEvaluations;
        var itemData = agentFashion->Items;

        if (hints.Length != stamps.Length)
            return res;

        for (int i = 0; i < hints.Length; i++)
        {
            result.Categories.Add(new FashionReportCategory(hints[i], stamps[i]));
        }

        foreach (var item in itemData)
        {
            result.ItemIds.Add(ItemUtil.GetBaseId(item.ItemId).ItemId);

            var equipSlot = Sheets.GetItem(item.ItemId).EquipSlotCategory.RowId;
            if (equipSlot is (>= 1 and <= 8) or 13)
            {
                result.StainIds.AddRange(item.Stain0Id, item.Stain1Id);
            }
        }

        Plugin.UploadEntry(new Export.FashionReport(result));

        return res;
    }

    private void UpdateNumberDetour(AgentLotteryDaily* agent, int index, byte value)
    {
        UpdateNumberHook!.Original(agent, index, value);
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

    private void UpdatePayoutDetour(AgentLotteryDaily* agent, int sum, int mgp)
    {
        UpdatePayoutHook!.Original(agent, sum, mgp);

        try
        {
            if (LastDataSet == null)
            {
                Plugin.Log.Error("Reached UpdatePayout without creating LastDataSet?");
                return;
            }

            for (var i = 0; i < agent->Numbers.Length; i++)
                LastDataSet.FullBoard[i] = agent->Numbers[i];

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

    private void HandleSpawnNPCPacketDetour(uint targetId, SpawnNpcPacket* packet)
    {
        HandleSpawnNPCPacketHook!.Original(targetId, packet);

        try
        {
            if (Sheets.HousingTerritory.Contains(Plugin.ClientState.TerritoryType))
                return;

            if (packet->Common.ObjectKind == ObjectKind.Retainer)
                return;

            if (packet->Common.ObjectKind == ObjectKind.BattleNpc)
            {
                if ((BattleNpcSubKind)packet->Common.SubKind is BattleNpcSubKind.Pet or BattleNpcSubKind.Buddy or BattleNpcSubKind.RaceChocobo)
                    return;
            }

            if (Sheets.DisallowedBnpcBase.Contains(packet->Common.BaseId))
                return;

            var bnpcPairData = new Export.BnpcPair(packet, 1);
            if (UploadHashes.Add(bnpcPairData.Hashed))
                Plugin.UploadEntry(bnpcPairData);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error while processing HandleSpawnNPC.");
        }
    }

    private void HandleEventYieldPacketDetour(EventId eventId, short scene, byte yieldId, int* intParams, byte intParamCount)
    {
        HandleEventYieldPacketHook!.Original(eventId, scene, yieldId, intParams, intParamCount);

        if (eventId.ContentId != EventHandlerContent.GuildLeveAssignment)
            return;

        if (scene != 0 || yieldId != 0 || intParamCount == 0)
            return;

        try
        {
            var metaValue = (uint)intParams[0];
            var guildleveAssignmentCategory = (byte)(metaValue >> 24);
            var category = (byte)(metaValue >> 16);
            var count = (ushort)metaValue >> 7;

            var leveIds = new List<ushort>(count);
            for (var i = 0; i < count; i++)
            {
                var leveId = (ushort)(intParams[(i >> 1) + 2] >> (16 * ((i - 1) & 1)));
                leveIds.Add(leveId);
            }

            var data = new GuildleveAssignmentData
            {
                RowId = eventId.Id,
                CategoryRowId = guildleveAssignmentCategory,
                CategoryIndex = category,
                LeveIds = leveIds,
            };

            if (data.LeveIds.Count == 0)
                return;

            Plugin.UploadEntry(new Export.GuildleveAssignments(data));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error while processing HandleEventYieldPacket.");
        }
    }
}
