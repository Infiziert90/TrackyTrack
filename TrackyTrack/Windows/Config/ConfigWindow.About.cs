using System.Threading.Tasks;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace TrackyTrack.Windows.Config;

public partial class ConfigWindow
{
    private string InputPath = string.Empty;

    private void About()
    {
        using var tabItem = ImRaii.TabItem("About");
        if (!tabItem.Success)
            return;

        var buttonHeight = ImGui.CalcTextSize("RRRR").Y + (20.0f * ImGuiHelpers.GlobalScale);
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

                #if DEBUG
                ImGui.TextColored(ImGuiColors.DalamudViolet, "Input File:");
                ImGui.InputText("##InputPath", ref InputPath, 255);
                ImGui.SameLine(0, 3.0f * ImGuiHelpers.GlobalScale);
                if (ImGuiComponents.IconButton(FontAwesomeIcon.FolderClosed))
                    ImGui.OpenPopup("InputPathDialog");

                using var popup = ImRaii.Popup("InputPathDialog");
                if (popup.Success)
                    Plugin.FileDialogManager.OpenFileDialog("Pick a file", ".csv", (b, s) => { if (b) InputPath = s.First(); }, 1);

                if (ImGui.Button("Import Data"))
                {
                    Task.Run(() =>
                    {
                        Plugin.Importer.Import(InputPath);
                        Utils.AddNotification("Import Done", NotificationType.Success);
                    });
                }

                if (ImGui.Button("Import Duty Data"))
                {
                    Task.Run(() =>
                    {
                        Plugin.Importer.ImportDutyLoot(InputPath);
                        Utils.AddNotification("ImportDutyLoot Done", NotificationType.Success);
                    });
                }
                #endif
            }
        }

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(1.0f);

        using var bottomChild = ImRaii.Child("AboutBottomBar", new Vector2(0, 0), false, 0);
        if (!bottomChild.Success)
            return;

        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.ParsedBlue))
        {
            if (ImGui.Button("Discord Thread"))
                Dalamud.Utility.Util.OpenLink("https://canary.discord.com/channels/581875019861328007/1143510564165926992");
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
    }
}
