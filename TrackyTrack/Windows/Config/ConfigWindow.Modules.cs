using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace TrackyTrack.Windows.Config;

public partial class ConfigWindow
{
    private void Modules()
    {
        using var tabItem = ImRaii.TabItem("Modules");
        if (!tabItem.Success)
            return;

        ImGuiHelpers.ScaledDummy(5.0f);
        var changed = false;

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Normal:");
        using (ImRaii.PushIndent(10.0f))
        {
            changed |= ImGui.Checkbox("Currency Tracking", ref Configuration.EnableCurrency);
            ImGuiComponents.HelpMarker("Tracks the total amount over time." +
                                       "\n  - Grand Company Seals" +
                                       "\n  - MGP" +
                                       "\n  - Venture Coins" +
                                       "\n  - Allied Seals" +
                                       "\n  - Centurio Seals" +
                                       "\n  - Sack Of Nuts" +
                                       "\n  - Bicolor Gemstone" +
                                       "\n  - Skybuilders' Scrip");
            changed |= ImGui.Checkbox("Repair Cost Tracking", ref Configuration.EnableRepair);
            ImGuiComponents.HelpMarker("Tracks repairs done at any Mender NPC." +
                                       "\nNo support for repairs using the skill and other players");
            changed |= ImGui.Checkbox("Teleport Cost Tracking", ref Configuration.EnableTeleport);
            ImGuiComponents.HelpMarker("Tracks the total amount of teleports done." +
                                       "\nAlso tracks the following tickets:" +
                                       "\n  - Aetheryte Tickets" +
                                       "\n  - Grand Company Tickets" +
                                       "\n  - Vesper Bay Tickets" +
                                       "\n  - Firmament Tickets");
            changed |= ImGui.Checkbox("Desynthesis Tracking", ref Configuration.EnableDesynthesis);
            changed |= ImGui.Checkbox("Retainer Venture Tracking", ref Configuration.EnableRetainer);
        }

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Advanced:");
        using (ImRaii.PushIndent(10.0f))
        {
            changed |= ImGui.Checkbox("Bulk Desynthesis Support", ref Configuration.EnableBulkSupport);
            changed |= ImGui.Checkbox("Venture Coffer Tracking", ref Configuration.EnableVentureCoffers);
            changed |= ImGui.Checkbox("Gacha Coffer Tracking", ref Configuration.EnableGachaCoffers);
            changed |= ImGui.Checkbox("Bunny Coffer Tracking", ref Configuration.EnableEurekaCoffers);
            changed |= ImGui.Checkbox("Lockbox Tracking", ref Configuration.EnableLockboxes);
        }

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Optional:");
        using (ImRaii.PushIndent(10.0f))
        {
            changed |= ImGui.Checkbox("Show Unlocked Checkmark", ref Configuration.ShowUnlockCheckmark);
            ImGuiComponents.HelpMarker("Only for Gacha 3.0 and Gacha 4.0.");
        }

        if (changed)
        {
            Plugin.FrameworkManager.IsSafe = false;
            Configuration.Save();
        }
    }
}
