using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TrackyTrack.Data;

namespace TrackyTrack.Manager;

public class FrameworkManager
{
    private readonly Plugin Plugin;

    private static readonly Dictionary<Currency, int> CurrencyCounts = new()
    {
        { Currency.Gil, 0 },             // Gil
        { Currency.StormSeals, 0 },      // Storm Seals
        { Currency.SerpentSeals, 0 },    // Serpent Seals
        { Currency.FlameSeals, 0 },      // Flame Seals
        { Currency.MGP, 0 },             // MGP
        { Currency.AlliedSeals, 0 },     // Allied Seals
        { Currency.Ventures, 0 },        // Venture
        { Currency.SackOfNuts, 0 },      // Sack of Nuts
        { Currency.CenturioSeals, 0 },   // Centurio Seals
        { Currency.Bicolor, 0 },         // Bicolor
        { Currency.Skybuilders, 0 },     // Skybuilders
    };

    public bool IsSafe;
    private uint LastSeenVentureId;

    public FrameworkManager(Plugin plugin)
    {
        Plugin = plugin;

        Plugin.Framework.Update += TicketTracker;
        Plugin.Framework.Update += CurrencyTracker;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= TicketTracker;
        Plugin.Framework.Update -= CurrencyTracker;
    }

    public unsafe void ScanCurrentCharacter()
    {
        var instance = InventoryManager.Instance();
        if (instance == null)
            return;

        foreach (var currency in CurrencyCounts.Keys)
            CurrencyCounts[currency] = instance->GetInventoryItemCount((uint) currency, false, false, false);

        IsSafe = true;
    }

    public int Type;
    public int NodeLevel;
    public void GatheringNodeOpening(AddonEvent addonEvent, AddonArgs addonArgs)
    {
        try
        {
            Type = -1;
            if (Plugin.ClientState.LocalPlayer is { TargetObject: not null } player)
            {
                var node = Sheets.GatheringPoints.GetRow(player.TargetObject.DataId)!;
                Type = node.Type;
                NodeLevel = node.GatheringPointBase.Value!.GatheringLevel;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e, "Unable to read gathering node type");
        }
    }

    public void GatheringNodeClosing(AddonEvent addonEvent, AddonArgs addonArgs)
    {
        try
        {
            CheckNode(addonArgs, 9);
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e, "Parsing gathering node close failed");
        }
    }

    public void MasterpieceNodeClosing(AddonEvent addonEvent, AddonArgs addonArgs)
    {
        try
        {
            CheckNode(addonArgs, 126);
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e, "Parsing gathering node close failed");
        }
    }

    public unsafe void CheckNode(AddonArgs addonArgs, uint textIndex)
    {
        var addon = (AtkUnitBase*)addonArgs.Addon;
        var text = addon->GetTextNodeById(textIndex);
        var successfulGathered = text->NodeText.ToInteger() == 0;

        if (!successfulGathered)
            return;

        if (Plugin.TimerManager.Revisited < 1)
            Plugin.TimerManager.StartRevisit();
        else
            Plugin.TimerManager.Revisited = -1;
    }

    public unsafe void RetainerPreChecker(AddonEvent addonEvent, AddonArgs addonArgs)
    {
        var manager = RetainerManager.Instance();
        if (manager != null)
        {
            try
            {
                var retainer = manager->GetActiveRetainer();
                if (addonArgs.AddonName == "SelectString" && retainer != null)
                    LastSeenVentureId = retainer->VentureId;
            }
            catch (Exception e)
            {
                Plugin.Log.Warning(e.Message);
                Plugin.Log.Warning(e.StackTrace ?? "Unknown");
            }
        }
    }

    public unsafe void RetainerChecker(AddonEvent addonEvent, AddonArgs addonArgs)
    {
        var venture = AgentRetainerTask.Instance();
        if (venture != null)
        {
            if (addonArgs.AddonName == "RetainerTaskResult")
            {
                try
                {
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

                    Plugin.RetainerHandler(LastSeenVentureId, new VentureItem(primary, primaryCount, primaryHQ),
                                           new VentureItem(additionalItem, additionalCount, additionalHQ));
                }
                catch (Exception e)
                {
                    Plugin.Log.Warning(e.Message);
                    Plugin.Log.Warning(e.StackTrace ?? "Unknown");
                }
            }
        }
    }

    public unsafe void CurrencyTracker(IFramework _)
    {
        // Only run for real characters
        if (Plugin.ClientState.LocalContentId == 0)
        {
            IsSafe = false;
            return;
        }

        if (!IsSafe)
        {
            ScanCurrentCharacter();
            return;
        }

        var instance = InventoryManager.Instance();
        if (instance == null)
            return;

        if (Plugin.Configuration.EnableRepair)
        {
            var currentGil = instance->GetInventoryItemCount((uint) Currency.Gil, false, false, false);
            if (currentGil < CurrencyCounts[Currency.Gil])
                Plugin.TimerManager.RepairResult(CurrencyCounts[Currency.Gil] - currentGil);
            CurrencyCounts[Currency.Gil] = currentGil;
        }

        if (Plugin.Configuration.EnableCurrency)
        {
            foreach (var (currency, oldCount) in CurrencyCounts)
            {
                var current = instance->GetInventoryItemCount((uint) currency, false, false, false);
                if (current > oldCount)
                    Plugin.CurrencyHandler(currency, current - oldCount);
                CurrencyCounts[currency] = current;
            }
        }
    }

    public void TicketTracker(IFramework _)
    {
        var local = Plugin.ClientState.LocalPlayer;
        if (local == null || !local.IsCasting)
            return;

        switch (local)
        {
            // Tickets
            case { CastActionId: 21069, CastActionType: 2 }:
            case { CastActionId: 21070, CastActionType: 2 }:
            case { CastActionId: 21071, CastActionType: 2 }:
            case { CastActionId: 30362, CastActionType: 2 }:
            case { CastActionId: 28064, CastActionType: 2 }:
            {
                if (Plugin.TimerManager.TicketUsedTimer.Enabled)
                    return;

                // 300ms before cast finish is when cast counts as successful
                if (local.CurrentCastTime + 0.300 > local.TotalCastTime)
                    Plugin.CastedTicketHandler(local.CastActionId);
                break;
            }
        }
    }
}
