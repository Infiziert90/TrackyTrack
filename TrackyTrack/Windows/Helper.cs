using Dalamud.Interface.Components;

namespace TrackyTrack.Windows;

public static class Helper
{
    public static void NoDesynthesisData()
    {
        ImGuiHelpers.ScaledDummy(10.0f);
        WrappedError("No data found for this character\nPlease desynthesis an item.");
    }

    public static void NoVentureCofferData()
    {
        ImGuiHelpers.ScaledDummy(10.0f);
        WrappedError("No data found for this character\nPlease open a venture coffer.");
    }

    public static void NoGachaData()
    {
        ImGuiHelpers.ScaledDummy(10.0f);
        WrappedError("No data found for this character\nPlease open a gacha coffer (from GC).");
    }

    public static void WrappedError(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);
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

    public static void DrawArrows(ref int selected, int length, int id = 0)
    {
        ImGui.SameLine();
        if (selected == 0) ImGui.BeginDisabled();
        if (ImGuiComponents.IconButton(id, FontAwesomeIcon.ArrowLeft)) selected--;
        if (selected == 0) ImGui.EndDisabled();

        ImGui.SameLine();
        if (selected + 1 == length) ImGui.BeginDisabled();
        if (ImGuiComponents.IconButton(id+1, FontAwesomeIcon.ArrowRight)) selected++;
        if (selected + 1 == length) ImGui.EndDisabled();
    }
}
