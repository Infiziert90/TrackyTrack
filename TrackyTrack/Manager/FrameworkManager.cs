using System.Globalization;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Text;
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

        Plugin.ChatGui.LogMessage += OnReductionLogMessage;
        Plugin.ChatGui.LogMessage += OnDesynthesisLogMessage;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= TicketTracker;
        Plugin.Framework.Update -= CurrencyTracker;
        Plugin.Framework.Update -= OccultTracker;
        Plugin.ChatGui.LogMessage -= OnReductionLogMessage;
        Plugin.ChatGui.LogMessage -= OnDesynthesisLogMessage;
    }

    private void OnReductionLogMessage(ILogMessage message)
    {
        // Check if message is Reduction start
        if (message.LogMessageId == 3553)
        {
            if (!CompareGstrPlayerNames(message.SourceEntity))
                return;

            // source + collectability
            if (message.ParameterCount != 2)
                return;

            var source = message.Parameters[0].UIntValue;
            var collectability = message.Parameters[1].UIntValue;

            Plugin.TimerManager.StartReduction(source, collectability);
            return;
        }

        if (!Plugin.TimerManager.LastReductionResult.AwaitingResults)
            return;

        if (message.LogMessageId == 3563)
            Plugin.TimerManager.LastReductionResult.SetBonus();

        if (message.LogMessageId is 3555 or 3554)
        {
            // reward + count
            if (message.ParameterCount is 1 or 2)
            {
                var reward = message.Parameters[0].UIntValue;
                var count = message.ParameterCount == 2 ? message.Parameters[1].UIntValue : 1;

                Plugin.TimerManager.LastReductionResult.AddItem(reward, count);
            }
        }
    }

    private void OnDesynthesisLogMessage(ILogMessage message)
    {
        // Check if message is Desynthesis start
        if (message.LogMessageId == 4321)
        {
            if (!CompareGstrPlayerNames(message.SourceEntity))
                return;

            // source
            if (message.ParameterCount != 1)
                return;

            var source = message.Parameters[0].UIntValue;

            Plugin.TimerManager.StartDesynthesis(source);
            return;
        }

        if (!Plugin.TimerManager.LastDesynthesisResult.AwaitingResults)
            return;

        if (message.LogMessageId == 4325)
        {
            // Job + Whole + Decimal
            if (message.ParameterCount == 3)
            {
                var integral = message.Parameters[1].UIntValue;
                var fractional = message.Parameters[2].UIntValue;
                var combined = double.Parse($"{integral}.{fractional:00}", CultureInfo.InvariantCulture);

                Plugin.TimerManager.LastDesynthesisResult.SetLevel(combined);
            }
        }

        if (message.LogMessageId is 4322 or 4323)
        {
            // Reward + Count
            if (message.ParameterCount is 1 or 2)
            {
                var reward = message.Parameters[0].UIntValue;
                var count = message.ParameterCount == 2 ? message.Parameters[1].UIntValue : 1;

                Plugin.TimerManager.LastDesynthesisResult.AddItem(reward, count);
            }
        }
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
        if (!Plugin.PlayerState.IsLoaded)
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
        var local = Plugin.ObjectTable.LocalPlayer;
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

    private readonly ushort[] OccultBunnyFates = [1976, 1977, 2072, 2073];
    private void OccultTracker(IFramework _)
    {
        var local = Plugin.ObjectTable.LocalPlayer;
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
        if (!OccultExtensions.RarityArray.Contains(target.BaseId))
            return;

        // 300ms before cast finish is when cast counts as successful
        if (local.CurrentCastTime + 0.300 > local.TotalCastTime)
        {
            Plugin.TimerManager.LastTargetBaseId = target.BaseId;
            Plugin.TimerManager.LastTargetPosition = target.Position;
        }
    }

    private bool CompareGstrPlayerNames(ILogMessageEntity? source)
    {
        if (source == null)
            return false;

        if (!TryGetGStr(0, out var gstr1))
            return false;

        return gstr1.SequenceEqual(source.Name.Data.Span);
    }

    private unsafe bool TryGetGStr(uint parameterIndex, out ReadOnlySpan<byte> gstr)
    {
        gstr = [];

        var rtm = RaptureTextModule.Instance();
        if (rtm is null)
            return false;

        ref var gp = ref rtm->GlobalParameters;
        if (parameterIndex >= gp.MySize)
            return false;

        if (!ThreadSafety.IsMainThread)
        {
            Plugin.Log.Error("Global parameters may only be used from the main thread.");
            return false;
        }

        ref var p = ref gp[parameterIndex];
        switch (p.Type)
        {
            case TextParameterType.ReferencedUtf8String:
                gstr = p.ReferencedUtf8StringValue->Utf8String.AsSpan();
                return true;
            case TextParameterType.String:
                gstr = p.StringValue.AsSpan();
                return true;
            case TextParameterType.Integer:
            case TextParameterType.Uninitialized:
            default:
                return false;
        }
    }
}
