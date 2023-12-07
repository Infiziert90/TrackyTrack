using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using TrackyTrack.Data;

namespace TrackyTrack.Manager;

public class FrameworkManager
{
    private readonly Plugin Plugin;

    public bool IsSafe;

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

    private uint LastSeenVentureId = 0;

    public FrameworkManager(Plugin plugin)
    {
        Plugin = plugin;

        Plugin.Framework.Update += TicketTracker;
        Plugin.Framework.Update += CurrencyTracker;
        Plugin.Framework.Update += EurekaTracker;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= TicketTracker;
        Plugin.Framework.Update -= CurrencyTracker;
        Plugin.Framework.Update -= EurekaTracker;
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

    public unsafe void RetainerPreChecker(AddonEvent addonEvent, AddonArgs addonArgs)
    {
        var manager = RetainerManager.Instance();
        if (manager != null)
        {
            try
            {
                var retainer = manager->GetActiveRetainer();
                if (addonArgs.AddonName == "SelectString" && retainer != null)
                    LastSeenVentureId = retainer->VentureID;
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

    public void EurekaTracker(IFramework _)
    {
        if (!Plugin.Configuration.EnableEurekaCoffers)
            return;

        if (!EurekaExtensions.AsArray.Contains(Plugin.ClientState.TerritoryType))
            return;

        var local = Plugin.ClientState.LocalPlayer;
        if (local == null || !local.IsCasting)
            return;

        // Interaction cast on coffer
        if (local is { CastActionId: 21, CastActionType: 4 })
        {
            if (Plugin.TimerManager.AwaitingEurekaResult.Enabled)
                return;

            if (local.TargetObject == null)
                return;

            // 800ms before cast finish is when cast counts as successful
            // Increased from 100 to 800 because people with high ping having issues
            if (local.CurrentCastTime + 0.800 > local.TotalCastTime)
            {
                Plugin.Log.Debug($"Successful opening {((CofferRarity) local.TargetObject.DataId).ToName()}");
                Plugin.TimerManager.StartEureka(local.TargetObject.DataId);
            }
        }
    }
}
