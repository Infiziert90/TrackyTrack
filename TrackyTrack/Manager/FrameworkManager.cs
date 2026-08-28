using System.Globalization;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
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
        Plugin.Framework.Update += BunnyTracker;

        Plugin.ChatGui.LogMessage += OnRouletteBonus;
        Plugin.ChatGui.LogMessage += OnEurekaBunnyMessage;
        Plugin.ChatGui.LogMessage += OnOccultTreasureMessage;
        Plugin.ChatGui.LogMessage += OnOccultPotMessage;
        Plugin.ChatGui.LogMessage += OnReductionLogMessage;
        Plugin.ChatGui.LogMessage += OnDesynthesisLogMessage;

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "FashionCheck", OnFashionCheckPostSetup);
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= TicketTracker;
        Plugin.Framework.Update -= CurrencyTracker;
        Plugin.Framework.Update -= OccultTracker;
        Plugin.Framework.Update -= BunnyTracker;
        Plugin.ChatGui.LogMessage -= OnRouletteBonus;
        Plugin.ChatGui.LogMessage -= OnEurekaBunnyMessage;
        Plugin.ChatGui.LogMessage -= OnOccultTreasureMessage;
        Plugin.ChatGui.LogMessage -= OnOccultPotMessage;
        Plugin.ChatGui.LogMessage -= OnReductionLogMessage;
        Plugin.ChatGui.LogMessage -= OnDesynthesisLogMessage;
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "FashionCheck", OnFashionCheckPostSetup);
    }

    private void OnRouletteBonus(ILogMessage message)
    {
        if (!Plugin.TempManager.CurrentRoulette.AwaitingResults)
            return;

        if (message.LogMessageId is 2246)
        {
            var exp = message.Parameters[0].UIntValue;
            var gil = message.Parameters[1].UIntValue;

            Plugin.TempManager.CurrentRoulette.AddBonus(exp, gil);

            var current = Plugin.TempManager.CurrentRoulette.Clone();
            Plugin.TempManager.CurrentRoulette = new RouletteData();

            if (!current.IsValid)
                return;

            var character = Plugin.CharacterStorage.GetOrCreate(Plugin.PlayerState.ContentId);
            character.Roulette.Total += 1;
            character.Roulette.History.Add(DateTime.Now, current);
            Plugin.ConfigurationBase.SaveCharacterConfig();

            Plugin.UploadEntry(new Export.RouletteReport(current));
        }
    }

    private void OnEurekaBunnyMessage(ILogMessage message)
    {
        if (!Plugin.TimerManager.EurekaCoffer.AwaitingResults)
            return;

        if (message.LogMessageId is 1233 or 1232)
        {
            var reward = message.Parameters[0].UIntValue;
            var count = message.LogMessageId == 1233 ? message.Parameters[1].UIntValue : 1;

            Plugin.TimerManager.EurekaCoffer.AddItem(reward, count);
        }
    }

    private void OnOccultTreasureMessage(ILogMessage message)
    {
        if (!Plugin.TimerManager.OccultCoffer.AwaitingResults)
            return;

        if (Plugin.Configuration.Debugging)
            Plugin.Log.Debug($"{message.LogMessageId} | {message.FormatLogMessageForDebugging()}");

        if (message.LogMessageId is 1233 or 1232)
        {
            var reward = message.Parameters[0].UIntValue;
            var count = message.LogMessageId == 1233 ? message.Parameters[1].UIntValue : 1;

            Plugin.TimerManager.OccultCoffer.AddItem(reward, count);
        }

        if (message.LogMessageId is 4592)
        {
            var reward = message.Parameters[1].UIntValue;
            var count = message.Parameters[0].UIntValue;

            Plugin.TimerManager.OccultCoffer.AddItem(reward, count);
        }

        // if (message.LogMessageId is 11395)
        // {
        //     Plugin.Log.Debug($"message: {message.LogMessageId} | {message.FormatLogMessageForDebugging()}");
        // }
    }

    private void OnOccultPotMessage(ILogMessage message)
    {
        if (!Plugin.TimerManager.OccultPot.AwaitingResults)
            return;

        if (message.LogMessageId is 1233 or 1232)
        {
            var reward = message.Parameters[0].UIntValue;
            var count = message.LogMessageId == 1233 ? message.Parameters[1].UIntValue : 1;

            Plugin.TimerManager.OccultPot.AddItem(reward, count);
        }

        if (message.LogMessageId is 4592)
        {
            var reward = message.Parameters[1].UIntValue;
            var count = message.Parameters[0].UIntValue;

            Plugin.TimerManager.OccultPot.AddItem(reward, count);
        }
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

    private unsafe void OnFashionCheckPostSetup(AddonEvent type, AddonArgs args)
    {
        var agentFashion = AgentFashion.Instance();
        if (agentFashion != null && agentFashion->OpenType != AgentFashionOpenType.Result)
            return;

        var result = new FashionReportResult
        {
            WeekNum = agentFashion->FashionCheckData.WeeklyTheme - 9u,
            Score = agentFashion->FashionCheckData.Score
        };

        var hints = agentFashion->FashionCheckData.ItemThemes;
        var stamps = agentFashion->FashionCheckData.ItemEvaluations;
        var itemData = agentFashion->Items;

        if (hints.Length != stamps.Length)
            return;

        for (int i = 0; i < hints.Length; i++)
        {
            result.Categories.Add(new FashionReportCategory(hints[i], stamps[i]));
        }

        foreach (var item in itemData)
        {
            result.ItemIds.Add(item.ItemId);

            var equipSlot = Sheets.GetItem(item.ItemId).EquipSlotCategory.RowId;
            if (equipSlot is (>= 1 and <= 8) or 13)
            {
                result.StainIds.AddRange(item.Stain0Id, item.Stain1Id);
            }
        }

        Plugin.UploadEntry(new Export.FashionReport(result));
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

    private void BunnyTracker(IFramework _)
    {
        if (!EnumHelper.PlayerInEureka())
            return;

        var local = Plugin.ObjectTable.LocalPlayer;
        if (local is null)
            return;

        // Opening a chest is counted as cast animation, so early return if no cast
        if (!local.IsCasting)
            return;

        // Check if target is an EventObject, all bunny coffers are
        if (Plugin.TargetManager.Target is not { ObjectKind: ObjectKind.EventObj } target)
            return;

        // Check that current target is a bunny coffer
        if (!EurekaExtensions.RarityArray.Contains(target.BaseId))
            return;

        // We already have a timer running, just wait
        if (Plugin.TimerManager.EurekaCoffer.AwaitingResults)
            return;

        // 300ms before cast finish is when cast counts as successful
        if (local.CurrentCastTime + 0.300 > local.TotalCastTime)
            Plugin.TimerManager.StartEureka(target.BaseId);
    }

    private readonly ushort[] OccultBunnyFates = [1976, 1977, 2072, 2073];
    private void OccultTracker(IFramework _)
    {
        if (!EnumHelper.PlayerInOccult())
            return;

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

        // We already have a timer running, just wait
        if (Plugin.TimerManager.OccultPot.AwaitingResults)
            return;

        // 300ms before cast finish is when cast counts as successful
        if (local.CurrentCastTime + 0.300 > local.TotalCastTime)
            Plugin.TimerManager.StartOccultPot(target.BaseId, target.Position);
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
