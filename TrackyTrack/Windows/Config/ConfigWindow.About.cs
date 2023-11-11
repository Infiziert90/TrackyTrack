using System.Threading.Tasks;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;

namespace TrackyTrack.Windows.Config;

public partial class ConfigWindow
{
    private string InputPath = string.Empty;

    private void About()
    {
        if (ImGui.BeginTabItem("About"))
        {
            var buttonHeight = ImGui.CalcTextSize("RRRR").Y + (20.0f * ImGuiHelpers.GlobalScale);
            if (ImGui.BeginChild("AboutContent", new Vector2(0, -buttonHeight)))
            {
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.TextUnformatted("Author:");
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.ParsedGold, Plugin.Authors);

                ImGui.TextUnformatted("Discord:");
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.ParsedGold, "@infi");

                ImGui.TextUnformatted("Version:");
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.ParsedOrange, Plugin.Version);

                #if DEBUG
                ImGui.TextColored(ImGuiColors.DalamudViolet, "Input File:");
                ImGui.InputText("##InputPath", ref InputPath, 255);
                ImGui.SameLine(0, 3.0f * ImGuiHelpers.GlobalScale);
                if (ImGuiComponents.IconButton(FontAwesomeIcon.FolderClosed))
                    ImGui.OpenPopup("InputPathDialog");

                if (ImGui.BeginPopup("InputPathDialog"))
                {
                    Plugin.FileDialogManager.OpenFileDialog(
                        "Pick a file",
                        ".csv",
                        (b, s) => { if (b) InputPath = s.First(); },
                        1);

                    ImGui.EndPopup();
                }

                if (ImGui.Button("Import Data"))
                {
                    Task.Run(() =>
                    {
                        Plugin.Importer.Import(InputPath);
                        Plugin.PluginInterface.UiBuilder.AddNotification("Import Done", "[Tracky]", NotificationType.Success);
                    });
                }
                #endif
            }
            ImGui.EndChild();

            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(1.0f);

            if (ImGui.BeginChild("AboutBottomBar", new Vector2(0, 0), false, 0))
            {
                ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.ParsedBlue);
                if (ImGui.Button("Discord Thread"))
                    Dalamud.Utility.Util.OpenLink("https://canary.discord.com/channels/581875019861328007/1143510564165926992");
                ImGui.PopStyleColor();

                ImGui.SameLine();

                ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DPSRed);
                if (ImGui.Button("Issues"))
                    Dalamud.Utility.Util.OpenLink("https://github.com/Infiziert90/TrackyTrack/issues");
                ImGui.PopStyleColor();

                ImGui.SameLine();

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.12549f, 0.74902f, 0.33333f, 0.6f));
                if (ImGui.Button("Ko-Fi Tip"))
                    Dalamud.Utility.Util.OpenLink("https://ko-fi.com/infiii");
                ImGui.PopStyleColor();
            }
            ImGui.EndChild();

            ImGui.EndTabItem();
        }
    }
}
