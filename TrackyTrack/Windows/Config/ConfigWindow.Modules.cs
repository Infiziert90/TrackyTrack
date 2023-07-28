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
            changed |= ImGui.Checkbox("Repair Cost Tracking", ref Configuration.EnableRepair);
            changed |= ImGui.Checkbox("Teleport Cost Tracking", ref Configuration.EnableTeleport);
            changed |= ImGui.Checkbox("Grand Company Seal Tracking", ref Configuration.EnableCurrency);
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
