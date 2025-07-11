﻿using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
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

    public FrameworkManager(Plugin plugin)
    {
        Plugin = plugin;

        Plugin.Framework.Update += TicketTracker;
        Plugin.Framework.Update += CurrencyTracker;
        Plugin.Framework.Update += OccultTracker;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= TicketTracker;
        Plugin.Framework.Update -= CurrencyTracker;
        Plugin.Framework.Update -= OccultTracker;
    }

    private unsafe void ScanCurrentCharacter()
    {
        var instance = InventoryManager.Instance();
        if (instance == null)
            return;

        foreach (var currency in CurrencyCounts.Keys)
            CurrencyCounts[currency] = instance->GetInventoryItemCount((uint) currency, false, false, false);

        IsSafe = true;
    }

    private unsafe void CurrencyTracker(IFramework _)
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

    private void TicketTracker(IFramework _)
    {
        var local = Plugin.ClientState.LocalPlayer;
        if (local is not { IsCasting: true })
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

    private readonly ushort[] OccultBunnyFates = [1976, 1977];
    private void OccultTracker(IFramework _)
    {
        var local = Plugin.ClientState.LocalPlayer;
        if (local is null)
            return;

        var activeBunnyFate = Plugin.FateTable.FirstOrDefault(f => OccultBunnyFates.Contains(f.FateId));
        if (activeBunnyFate != null)
            Plugin.TimerManager.LastBunnyFateId = activeBunnyFate.FateId;

        // Opening a chest is counted as cast animation, so early return if no cast
        if (!local.IsCasting)
            return;

        // Check if target is an EventObject, all bunny coffers are
        if (Plugin.TargetManager.Target is not { ObjectKind: ObjectKind.EventObj } target)
            return;

        // Check that current target is an occult coffer
        if (!OccultExtensions.AsArray.Contains(target.DataId))
            return;

        // 300ms before cast finish is when cast counts as successful
        if (local.CurrentCastTime + 0.300 > local.TotalCastTime)
        {
            Plugin.TimerManager.LastTargetBaseId = target.DataId;
            Plugin.TimerManager.LastTargetPosition = target.Position;
        }
    }
}
