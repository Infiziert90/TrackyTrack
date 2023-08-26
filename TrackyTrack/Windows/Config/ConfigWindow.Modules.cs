using Dalamud.Interface.Components;

namespace TrackyTrack.Windows.Config;

public partial class ConfigWindow
{
    private void Modules()
    {
        if (ImGui.BeginTabItem("Modules"))
        {
            ImGuiHelpers.ScaledDummy(5.0f);
            var changed = false;

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Normal:");
            ImGui.Indent(10.0f);
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
            ImGui.Unindent(10.0f);

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.TextColored(ImGuiColors.DalamudViolet, $"Advanced:");
            ImGui.Indent(10.0f);
            changed |= ImGui.Checkbox("Bulk Desynthesis Support", ref Configuration.EnableBulkSupport);
            changed |= ImGui.Checkbox("Venture Coffer Tracking", ref Configuration.EnableVentureCoffers);
            changed |= ImGui.Checkbox("Gacha Coffer Tracking", ref Configuration.EnableGachaCoffers);
            changed |= ImGui.Checkbox("Bunny Coffer Tracking", ref Configuration.EnableEurekaCoffers);
            ImGui.Unindent(10.0f);

            if (changed)
            {
                Plugin.FrameworkManager.IsSafe = false;
                Configuration.Save();
            }

            ImGui.EndTabItem();
        }
    }
}
