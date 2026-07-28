using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using TrackyTrack.Data;

namespace TrackyTrack.Manager;

[InlineArray(3)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FixedSizeArray3<T> where T : unmanaged
{
    private T _element0;
}

[InlineArray(5)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FixedSizeArray5<T> where T : unmanaged
{
    private T _element0;
}

[StructLayout(LayoutKind.Explicit, Size = 0x168)]
public unsafe partial struct Reward {
    [FieldOffset(0x00)] public RewardType Type;
    [FieldOffset(0x01)] public byte IsSuccess;
    [FieldOffset(0x08)] public Utf8String Name;
    [FieldOffset(0x70)] public uint Icon;
    [FieldOffset(0x74)] public uint Medal;
    // For GoldSaucerReward the Id is the index in this array of Addon RowIds: 9980, 9981, 9982, 9984, 9983, 9986, 9985, 9987, 9988, 9989, 9990, 9991, 9992, 9993, 9994, 9995, 9996
    // For WKSReward the Id is a byte, followed by a byte with flags
    [FieldOffset(0x78)] public uint Id;
    [FieldOffset(0x7C)] public byte EurekaFate;
    [FieldOffset(0x80)] public uint Experience; // Experience, Island EXP, ...
    [FieldOffset(0x84)] public byte ExperienceFlags;
    [FieldOffset(0x88)] public uint CurrencyAmount; // Gil, Seafarer's Cowrie, ...
    [FieldOffset(0x8C)] public byte CurrencyFlags;
    [FieldOffset(0x90)] internal FixedSizeArray5<ItemReward> _items;
    [FieldOffset(0x108)] public byte FateTokenTypeId;
    [FieldOffset(0x10C)] public uint FateTokenTypeItemId;
    [FieldOffset(0x110)] public uint FateTokenTypeAmount;
    [FieldOffset(0x118)] public void* FateTokenTypeItemRow;
    [FieldOffset(0x120)] public byte FateTokenTypeFlags;
    [FieldOffset(0x128)] public byte GrandCompany;
    [FieldOffset(0x12C)] public uint GCSealsAmount;
    [FieldOffset(0x130)] internal FixedSizeArray3<AdditionalItemReward> _additionalItems;
    [FieldOffset(0x160)] public byte ItemProcessedBits;
    [FieldOffset(0x161)] public byte ItemProcessedCount;

    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    public unsafe struct ItemReward {
        [FieldOffset(0x00)] public uint ItemId;
        [FieldOffset(0x04)] public uint Amount;
        [FieldOffset(0x08)] public void* ItemRow;
        [FieldOffset(0x10)] public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public unsafe struct AdditionalItemReward {
        [FieldOffset(0x00)] public uint ItemId;
        [FieldOffset(0x04)] public uint Amount;
        [FieldOffset(0x08)] public void* ItemRow;
    }
}

public enum RewardType : byte {
    FateReward = 0,
    Unk1 = 1, // ContentReward?
    Unk2 = 2, // TreasureHuntReward?
    GoldSaucerReward = 3,
    MJIReward = 4,
    WKSReward = 5,
}

public unsafe class HookManager
{
    private readonly Plugin Plugin;

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

    private const string TreasureInteractSig = "E9 ?? ?? ?? ?? 48 63 05";
    private delegate void TreasureInteractDelegate(Loot* loot, Treasure* treasureObj);
    private Hook<TreasureInteractDelegate> TreasureInteractHook;

    // Replace with https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentFateReward.cs
    private const string EnqueueRewardSig = "E8 ?? ?? ?? ?? 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8B 8D ?? ?? ?? ?? 48 33 CC E8 ?? ?? ?? ?? 48 81 C4 ?? ?? ?? ?? 41 5E";
    private delegate void EnqueueRewardDelegate(nint agentFateReward, Reward* reward);
    private Hook<EnqueueRewardDelegate> EnqueueRewardHook;

    private Hook<AgentLotteryDaily.Delegates.UpdateNumber>? UpdateNumberHook { get; init; }
    private Hook<AgentLotteryDaily.Delegates.UpdatePayout>? UpdatePayoutHook { get; init; }
    private Hook<PacketDispatcher.Delegates.HandleSpawnNpcPacket>? HandleSpawnNPCPacketHook { get; init; }
    // private Hook<AgentFateReward.Delegates.EnqueueReward>? EnqueueRewardHook { get; init; }

    public uint LastSeenItemId;
    private MiniCactpotData? LastDataSet;

    public readonly HashSet<string> UploadHashes = [];

    public HookManager(Plugin plugin)
    {
        Plugin = plugin;

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

        var enqueueRewardPtr = Plugin.SigScanner.ScanText(EnqueueRewardSig);
        EnqueueRewardHook = Plugin.Hook.HookFromAddress<EnqueueRewardDelegate>(enqueueRewardPtr, EnqueueRewardDetour);
        EnqueueRewardHook.Enable();

        // EnqueueRewardHook = Plugin.Hook.HookFromAddress<AgentFateReward.Delegates.EnqueueReward>(AgentFateReward.MemberFunctionPointers.EnqueueReward, EnqueueRewardDetour);
        // EnqueueRewardHook.Enable();

        UpdateNumberHook = Plugin.Hook.HookFromAddress<AgentLotteryDaily.Delegates.UpdateNumber>(AgentLotteryDaily.MemberFunctionPointers.UpdateNumber, UpdateNumberDetour);
        UpdateNumberHook.Enable();

        UpdatePayoutHook = Plugin.Hook.HookFromAddress<AgentLotteryDaily.Delegates.UpdatePayout>(AgentLotteryDaily.MemberFunctionPointers.UpdatePayout, UpdatePayoutDetour);
        UpdatePayoutHook.Enable();

        HandleSpawnNPCPacketHook = Plugin.Hook.HookFromAddress<PacketDispatcher.Delegates.HandleSpawnNpcPacket>(PacketDispatcher.MemberFunctionPointers.HandleSpawnNpcPacket, HandleSpawnNPCPacketDetour);
        HandleSpawnNPCPacketHook.Enable();
    }

    public void Dispose()
    {
        ActorControlSelfHook.Dispose();
        OpenInspectHook.Dispose();
        LootAddedHook.Dispose();
        RetainerTaskHook.Dispose();
        TreasureInteractHook.Dispose();
        EnqueueRewardHook?.Dispose();
        UpdateNumberHook?.Dispose();
        UpdatePayoutHook?.Dispose();
        HandleSpawnNPCPacketHook?.Dispose();
    }

    private void EnqueueRewardDetour(nint agentFateReward, Reward* reward)
    {
        try
        {
            EnqueueRewardHook!.Original(agentFateReward, reward);
            Plugin.UploadEntry(new Export.FateReward(reward));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Unable to enqueue reward.");
        }
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
            Plugin.Log.Information($"Interacting with {treasureObj->BaseId}");

            var baseId = treasureObj->BaseId;
            if ((OccultTerritory)Plugin.ClientState.TerritoryType == OccultTerritory.SouthHorn)
            {
                if (baseId is > 1856 or < 1789)
                    return;
            }
            else
            {
                if (baseId is > 2073 or < 2006)
                    return;
            }

            Plugin.TimerManager.StartTreasure(baseId, treasureObj->Position);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Unable to track treasure interaction.");
        }
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
}
