using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace TrackyTrack.Windows.Config;

public partial class ConfigWindow
{
    private void About()
    {
        using var tabItem = ImRaii.TabItem("About");
        if (!tabItem.Success)
            return;

        var buttonHeight = Helper.CalculateChildHeight();
        using (var contentChild = ImRaii.Child("AboutContent", new Vector2(0, -buttonHeight)))
        {
            if (contentChild.Success)
            {
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.TextUnformatted("Author:");
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.ParsedGold, Plugin.PluginInterface.Manifest.Author);

                ImGui.TextUnformatted("Discord:");
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.ParsedGold, "@infi");

                ImGui.TextUnformatted("Version:");
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.ParsedOrange, Plugin.PluginInterface.Manifest.AssemblyVersion.ToString());
            }
        }

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(Helper.SeparatorPadding);

        using var bottomChild = ImRaii.Child("AboutBottomBar", Vector2.Zero);
        if (!bottomChild.Success)
            return;

        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.ParsedBlue))
        {
            if (ImGui.Button("Discord Thread"))
                Dalamud.Utility.Util.OpenLink("https://discord.com/channels/581875019861328007/1143510564165926992");
        }

        ImGui.SameLine();

        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DPSRed))
        {
            if (ImGui.Button("Issues"))
                Dalamud.Utility.Util.OpenLink("https://github.com/Infiziert90/TrackyTrack/issues");
        }

        ImGui.SameLine();

        using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.12549f, 0.74902f, 0.33333f, 0.6f)))
        {
            if (ImGui.Button("Ko-Fi Tip"))
                Dalamud.Utility.Util.OpenLink("https://ko-fi.com/infiii");
        }

        ImGui.SameLine();

        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DalamudOrange))
        {
            if (ImGui.Button("Website"))
                Dalamud.Utility.Util.OpenLink("https://xivstats.com/");
        }
    }
}
