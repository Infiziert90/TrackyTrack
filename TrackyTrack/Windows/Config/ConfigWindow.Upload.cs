using Dalamud.Interface.Utility;

namespace TrackyTrack.Windows.Config;

public partial class ConfigWindow
{
    private void Upload()
    {
        if (ImGui.BeginTabItem("Upload"))
        {
            var changed = false;
            ImGuiHelpers.ScaledDummy(5.0f);

            Helper.WrappedText(ImGuiColors.DalamudViolet, "Anonymously provide data for contents.\nThis data can't be tied to you in any way and everyone benefits!");

            ImGui.TextColored(ImGuiColors.DalamudViolet, "What data?");
            ImGuiHelpers.ScaledIndent(10.0f);
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Venture Coffer");
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Grand Company Gacha");
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Sanctuary Gacha");
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Lockboxes");
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Bunny Coffers");
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Desynthesis");
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Gathering");
            ImGuiHelpers.ScaledIndent(-10.0f);

            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.TextColored(ImGuiColors.DalamudViolet, "If you'd like to see the results");
            if (ImGui.Button("Click Me"))
                Dalamud.Utility.Util.OpenLink("https://docs.google.com/spreadsheets/d/1VfncSL5gf9E7ehgND5nZgguUyUAmZiAMbQllLKcoxTQ/edit?usp=sharing");

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

            changed |= ImGui.Checkbox("Upload Permission", ref Configuration.UploadPermission);

            if (changed)
                Configuration.Save();

            ImGui.EndTabItem();
        }
    }
}
