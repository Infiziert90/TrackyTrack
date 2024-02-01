using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;

using static OtterGui.Classes.ToggleButton;

namespace TrackyTrack.Windows;

public static class Helper
{
    public static readonly Vector2 IconSize = new(28, 28);

    public static void NoCharacters()
    {
        ImGuiHelpers.ScaledDummy(10.0f);
        WrappedError("No characters found\nPlease teleport anywhere.");
    }

    public static void NoDesynthesisData()
    {
        ImGuiHelpers.ScaledDummy(10.0f);
        WrappedError("No data stored for desynthesis\nPlease desynthesis an item.");
    }

    public static void NoRetainerData()
    {
        ImGuiHelpers.ScaledDummy(10.0f);
        WrappedError("No data stored for retainers\nPlease complete a venture with your retainer.");
    }

    public static void NoVentureCofferData()
    {
        ImGuiHelpers.ScaledDummy(10.0f);
        WrappedError("No data stored for venture coffers\nPlease open a venture coffer.");
    }

    public static void NoGachaData(string cofferType)
    {
        ImGuiHelpers.ScaledDummy(10.0f);
        WrappedError($"No data stored for {cofferType} coffers\nPlease open a {cofferType} coffer.");
    }

    public static void NoEurekaCofferData()
    {
        ImGuiHelpers.ScaledDummy(10.0f);
        WrappedError("No data stored for bunny coffers\nPlease open a bunny coffer in eureka.");
    }

    public static void WrappedError(string text)
    {
        WrappedText(ImGuiColors.DalamudOrange, text);
    }

    public static void WrappedText(Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    public static void MainMenuIcon(Plugin plugin)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SameLine(avail - (60.0f * ImGuiHelpers.GlobalScale));

        if (ImGuiComponents.IconButton(FontAwesomeIcon.Sync))
            plugin.ConfigurationBase.Load();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Reloads all data from disk");

        ImGui.SameLine(avail - (33.0f * ImGuiHelpers.GlobalScale));

        if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
            plugin.DrawConfigUI();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Open the config menu");
    }

    public static bool DrawArrows(ref int selected, int length, int id = 0)
    {
        var changed = false;

        ImGui.SameLine();
        if (selected == 0) ImGui.BeginDisabled();
        if (ImGuiComponents.IconButton(id, FontAwesomeIcon.ArrowLeft))
        {
            selected--;
            changed = true;
        }
        if (selected == 0) ImGui.EndDisabled();

        ImGui.SameLine();
        if (selected + 1 == length) ImGui.BeginDisabled();
        if (ImGuiComponents.IconButton(id + 1, FontAwesomeIcon.ArrowRight))
        {
            selected++;
            changed = true;
        }
        if (selected + 1 == length) ImGui.EndDisabled();

        return changed;
    }

    public static void IconHeader(uint icon, Vector2 iconSize, string text, Vector4 textColor)
    {
        iconSize *= ImGuiHelpers.GlobalScale;

        DrawIcon(icon, iconSize);
        ImGui.SameLine();

        var textY = ImGui.CalcTextSize(text).Y;
        var cursorY = ImGui.GetCursorPosY();
        ImGui.SetCursorPosY(cursorY + iconSize.Y - textY);
        ImGui.TextColored(textColor, text);

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);
    }

    public static void RightAlignedText(string text, float indent = 0.0f)
    {
        indent *= ImGuiHelpers.GlobalScale;
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X + indent);
        ImGui.TextUnformatted(text);
    }

    public static void RightTextColored(Vector4 color, string text, float indent = 0.0f)
    {
        indent *= ImGuiHelpers.GlobalScale;
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X + indent);
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    public static void CenterText(string text, float indent = 0.0f)
    {
        indent *= ImGuiHelpers.GlobalScale;
        ImGui.SameLine(((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X) * 0.5f) + indent);
        ImGui.TextUnformatted(text);
    }

    public static void DrawIcon(uint iconId)
    {
        var size = IconSize * ImGuiHelpers.GlobalScale;
        var texture = Plugin.Texture.GetIcon(iconId);
        if (texture == null)
        {
            ImGui.Text($"Unknown icon {iconId}");
            return;
        }

        ImGui.Image(texture.ImGuiHandle, size);
    }

    public static void DrawIcon(uint iconId, Vector2 size)
    {
        var texture = Plugin.Texture.GetIcon(iconId);
        if (texture == null)
        {
            ImGui.Text($"Unknown icon {iconId}");
            return;
        }

        ImGui.Image(texture.ImGuiHandle, size);
    }

    public static void ToggleButton(ref int selected, params string[] labels)
    {
        var width = 0.0f;
        foreach (var label in labels)
            width = Math.Max(width, ImGui.CalcTextSize(label).X);

        var padding = 50.0f * ImGuiHelpers.GlobalScale;
        var size = new Vector2(width + padding, 0.0f);

        var pos = ImGui.GetCursorPos();
        for (var i = 0; i < labels.Length; i++)
        {
            var flags = i == 0 ? ImDrawFlags.RoundCornersLeft : i == labels.Length - 1 ? ImDrawFlags.RoundCornersRight : ImDrawFlags.RoundCornersNone;
            if (i > 0)
            {
                ImGui.SetCursorPos(pos with { X = pos.X + width + padding });
                pos = ImGui.GetCursorPos();
            }

            ActivatedButton(labels[i], size, ref selected, i, flags);
        }
    }

    public static void ToggleButton(string leftOption, string rightOption, ref int selected, float predefinedSize = 0.0f)
    {
        if (predefinedSize == 0.0f)
        {
            var leftSize = ImGui.CalcTextSize(leftOption);
            var rightSize = ImGui.CalcTextSize(rightOption);

            predefinedSize = Math.Max(leftSize.X, rightSize.X) + (50.0f * ImGuiHelpers.GlobalScale);
        }
        var size = new Vector2(predefinedSize, 0.0f);

        var pos = ImGui.GetCursorPos();
        ActivatedButton(leftOption, size, ref selected, 0, ImDrawFlags.RoundCornersLeft);
        ImGui.SetCursorPos(pos with { X = pos.X + predefinedSize });
        ActivatedButton(rightOption, size, ref selected, 1, ImDrawFlags.RoundCornersRight);
    }

    public static void ActivatedButton(string buttonText, Vector2 size, ref int selected, int number, ImDrawFlags corners)
    {
        var colors = ImGui.GetStyle().Colors;

        if (selected == number)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, colors[(int) ImGuiCol.ButtonActive]);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colors[(int) ImGuiCol.ButtonActive]);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colors[(int)ImGuiCol.ButtonHovered]  with { W = 0.4f });
        }

        if (ButtonEx(buttonText, size, ImGuiButtonFlags.None, corners))
            selected = number;

        ImGui.PopStyleColor(selected == number ? 2 : 1);
    }
}
