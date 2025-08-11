using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace TrackyTrack.Windows.Config;

public partial class ConfigWindow
{
    private void Upload()
    {
        using var tabItem = ImRaii.TabItem("Upload");
        if (!tabItem.Success)
            return;

        var changed = false;
        ImGuiHelpers.ScaledDummy(5.0f);

        Helper.WrappedText(ImGuiColors.DalamudViolet, "Anonymously provide data for contents.\nThis data can't be tied to you in any way and everyone benefits!");

        ImGui.TextColored(ImGuiColors.DalamudViolet, "What data?");
        using (ImRaii.PushIndent(10.0f))
        {
            ImGui.TextColored(ImGuiColors.DalamudViolet, "- Venture Coffer");
            ImGui.TextColored(ImGuiColors.DalamudViolet, "- Grand Company Gacha");
            ImGui.TextColored(ImGuiColors.DalamudViolet, "- Sanctuary Gacha");
            ImGui.TextColored(ImGuiColors.DalamudViolet, "- Lockboxes");
            ImGui.TextColored(ImGuiColors.DalamudViolet, "- Bunny Coffers");
            ImGui.TextColored(ImGuiColors.DalamudViolet, "- Desynthesis");
            ImGui.TextColored(ImGuiColors.DalamudViolet, "- Retainer Ventures");
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextColored(ImGuiColors.DalamudViolet, "If you'd like to see the results");
        if (ImGui.Button("Click Me"))
            Dalamud.Utility.Util.OpenLink("https://gacha.infi.ovh/");

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        changed |= ImGui.Checkbox("Upload Permission", ref Plugin.Configuration.UploadPermission);

        if (changed)
            Plugin.Configuration.Save();
    }
}
