using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace TrackyTrack.Manager;

public class FrameworkManager
{
    private Plugin Plugin;

    public bool IsSafe;

    private uint GilCount;
    private uint SealCount;
    private uint MGPCount;
    private uint AlliedSealsCount;


    public FrameworkManager(Plugin plugin)
    {
        Plugin = plugin;

        Plugin.Framework.Update += CofferTracker;
        Plugin.Framework.Update += CurrencyTracker;

        Plugin.ClientState.Login += ScanOnLogin;
        Plugin.ClientState.Logout += ResetOnLogout;

        if (Plugin.ClientState.IsLoggedIn)
            ScanCurrentCharacter();
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= CofferTracker;
        Plugin.Framework.Update -= CurrencyTracker;

        Plugin.ClientState.Login -= ScanOnLogin;
        Plugin.ClientState.Logout -= ResetOnLogout;
    }

    public void ScanOnLogin(object? _, EventArgs __) => ScanCurrentCharacter();
    public void ResetOnLogout(object? _, EventArgs __) => IsSafe = false;

    public unsafe void ScanCurrentCharacter()
    {
        IsSafe = false;

        var instance = InventoryManager.Instance();
        if (instance == null)
            return;

        var container = instance->GetInventoryContainer(InventoryType.Currency);
        GilCount = container->Items[0].Quantity;
        SealCount = container->Items[1].Quantity + container->Items[2].Quantity + container->Items[3].Quantity;
        MGPCount = container->Items[9].Quantity;
        AlliedSealsCount = container->Items[8].Quantity;

        IsSafe = true;
    }

    public unsafe void CurrencyTracker(Framework _)
    {
        if (!IsSafe)
        {
            ScanCurrentCharacter();
            return;
        }

        var instance = InventoryManager.Instance();
        if (instance == null)
            return;

        var container = instance->GetInventoryContainer(InventoryType.Currency);

        if (Plugin.Configuration.EnableRepair)
        {
            var currentGil = container->Items[0].Quantity;
            if (currentGil < GilCount)
                Plugin.TimerManager.RepairResult(GilCount - currentGil);
            GilCount = currentGil;
        }

        if (Plugin.Configuration.EnableCurrency)
        {
            var currentSeals = 0u;
            currentSeals += container->Items[1].Quantity;
            currentSeals += container->Items[2].Quantity;
            currentSeals += container->Items[3].Quantity;
            if (currentSeals > SealCount)
                Plugin.CurrencyHandler(20, currentSeals - SealCount);
            SealCount = currentSeals;

            var currentMGP = container->Items[9].Quantity;
            if (currentMGP > MGPCount)
                Plugin.CurrencyHandler(29, currentMGP - MGPCount);
            MGPCount = currentMGP;

            var currentAlliedSeals = container->Items[8].Quantity;
            if (currentAlliedSeals > AlliedSealsCount)
                Plugin.CurrencyHandler(27, currentAlliedSeals - AlliedSealsCount);
            AlliedSealsCount = currentAlliedSeals;
        }
    }

    public void CofferTracker(Framework _)
    {
        var local = Plugin.ClientState.LocalPlayer;
        if (local == null || !local.IsCasting)
            return;

        switch (local)
        {
            // Coffers
            case { CastActionId: 32161, CastActionType: 2 }:
            case { CastActionId: 36635, CastActionType: 2 }:
            case { CastActionId: 36636, CastActionType: 2 }:
            {
                if (Plugin.Configuration.EnableVentureCoffers || Plugin.Configuration.EnableGachaCoffers)
                    Plugin.TimerManager.StartCoffer();
                break;
            }

            // Tickets
            case { CastActionId: 21069, CastActionType: 2 }:
            case { CastActionId: 21070, CastActionType: 2 }:
            case { CastActionId: 21071, CastActionType: 2 }:
            case { CastActionId: 30362, CastActionType: 2 }:
            case { CastActionId: 28064, CastActionType: 2 }:
            {
                if (Plugin.TimerManager.TicketUsedTimer.Enabled)
                    return;

                // 100ms before cast finish is when cast counts as successful
                if (local.CurrentCastTime + 0.100 > local.TotalCastTime)
                    Plugin.CastedTicketHandler(local.CastActionId);
                break;
            }
        }
    }
}
