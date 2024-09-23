using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using static OtterGui.Widgets.ToggleButton;

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

    public static void OldHistory()
    {
        ImGuiHelpers.ScaledDummy(10.0f);
        WrappedError("All existing data is older than 30 days\nPlease complete a venture with your retainer.");
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
        using var pushedColor = ImRaii.PushColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(text);
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
            plugin.OpenConfigUi();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Open the config menu");
    }

    public static bool DrawArrows(ref int selected, int length, int id = 0)
    {
        var changed = false;

        // Prevents changing values from triggering EndDisable
        var isMin = selected == 0;
        var isMax = selected + 1 == length;

        ImGui.SameLine();

        using (ImRaii.Disabled(isMin))
        {
            if (ImGuiComponents.IconButton(id, FontAwesomeIcon.ArrowLeft))
            {
                selected--;
                changed = true;
            }
        }

        ImGui.SameLine();

        using (ImRaii.Disabled(isMax))
        {
            if (ImGuiComponents.IconButton(id + 1, FontAwesomeIcon.ArrowRight))
            {
                selected++;
                changed = true;
            }
        }

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

        using var textColor = ImRaii.PushColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
    }

    public static void CenterText(string text, float indent = 0.0f)
    {
        indent *= ImGuiHelpers.GlobalScale;
        ImGui.SameLine(((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X) * 0.5f) + indent);
        ImGui.TextUnformatted(text);
    }

    public static void DrawIcon(uint iconId, float withIndent = 0.0f)
    {
        using var indent = ImRaii.PushIndent(withIndent, condition: withIndent > 0.0f);

        var size = IconSize * ImGuiHelpers.GlobalScale;
        var texture = Plugin.Texture.GetFromGameIcon(iconId).GetWrapOrDefault();
        if (texture == null)
        {
            ImGui.Text($"Unknown icon {iconId}");
            return;
        }

        ImGui.Image(texture.ImGuiHandle, size);
    }

    public static void DrawIcon(uint iconId, Vector2 size, float withIndent = 0.0f)
    {
        using var indent = ImRaii.PushIndent(withIndent, condition: withIndent > 0.0f);

        var texture = Plugin.Texture.GetFromGameIcon(iconId).GetWrapOrDefault();
        if (texture == null)
        {
            ImGui.Text($"Unknown icon {iconId}");
            return;
        }

        ImGui.Image(texture.ImGuiHandle, size);
    }

    public static void HoverableText(string text)
    {
        ImGui.TextUnformatted(text);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(text);
    }

    public static void SelectableClipboardText(string name, float withIndent = 0.0f)
    {
        using var indent = ImRaii.PushIndent(withIndent, condition: withIndent > 0.0f);
        if (ImGui.Selectable(name))
            ImGui.SetClipboardText(name);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Click to copy");
    }

    public static void DrawUnlockedSymbol(bool unlocked)
    {
        using var font = Plugin.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push();
        using var textColor = ImRaii.PushColor(ImGuiCol.Text, unlocked ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed);

        ImGui.TextUnformatted(unlocked ? FontAwesomeIcon.Check.ToIconString() : FontAwesomeIcon.Times.ToIconString());
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

        var check = selected == number;
        using var buttonColor = ImRaii.PushColor(ImGuiCol.Button, colors[(int) ImGuiCol.ButtonActive], check);
        using var hoveredColor = ImRaii.PushColor(ImGuiCol.ButtonHovered, colors[(int) ImGuiCol.ButtonActive], check);
        using var negHoveredColor = ImRaii.PushColor(ImGuiCol.ButtonHovered, colors[(int)ImGuiCol.ButtonHovered]  with { W = 0.4f }, !check);

        if (ButtonEx(buttonText, size, ImGuiButtonFlags.None, corners))
            selected = number;
    }

    public static void ClippedCombo<T>(string label, ref int selected, T[] items, Func<T, string> toString)
    {
        var height = ImGui.GetTextLineHeightWithSpacing();

        using var combo = ImRaii.Combo(label, toString(items[selected]));
        if (!combo.Success)
            return;

        using var clipper = new ListClipper(items.Length, itemHeight: height);
        foreach (var idx in clipper.Rows)
            if (ImGui.Selectable(toString(items[idx]), idx == selected))
                selected = idx;
    }

    public static IOrderedEnumerable<T> NoSort<T>(IEnumerable<T> values, object _) => values.OrderBy(_ => 1);
}

public class SimpleTable<T>
{
    public record TableColumn(string Name, ImGuiTableColumnFlags Flags, float InitWidth);

    private readonly string TableName;
    private readonly float WithIndent;
    private readonly Func<IEnumerable<T>, object, IOrderedEnumerable<T>> Enumerator;
    private readonly ImGuiTableFlags Flags;
    private readonly Vector2 Size;

    private bool UseSortSpec;
    private bool ShowHeaderRow = true;

    private readonly List<TableColumn> Columns = [];
    private readonly List<Action<T>> ColumnActions = [];

    public SimpleTable(string tableName, Func<IEnumerable<T>, object, IOrderedEnumerable<T>> enumerator, ImGuiTableFlags flags = 0, Vector2? size = null, float withIndent = 0.0f)
    {
        TableName = tableName;
        Enumerator = enumerator;
        WithIndent = withIndent;
        Flags = flags;

        Size = size ?? Vector2.Zero;
    }

    public SimpleTable<T> AddColumn(string name, Action<T> columnAction, ImGuiTableColumnFlags flags = 0, float initWidth = 0, bool useColumn = true)
    {
        if (!useColumn)
            return this;

        Columns.Add(new TableColumn(name, flags, initWidth));
        ColumnActions.Add(columnAction);
        return this;
    }

    public SimpleTable<T> EnableSortSpec()
    {
        UseSortSpec = true;
        return this;
    }

    public SimpleTable<T> HideHeaderRow()
    {
        ShowHeaderRow = false;
        return this;
    }

    public void Draw(IEnumerable<T> values)
    {
        if (Columns.Count == 0)
            return;

        using var table = ImRaii.Table(TableName, Columns.Count, Flags, Size);
        if (!table.Success)
            return;

        foreach (var column in Columns)
            ImGui.TableSetupColumn(column.Name, column.Flags, column.InitWidth);

        if (ShowHeaderRow)
            ImGui.TableHeadersRow();

        using var indent = ImRaii.PushIndent(WithIndent, condition: WithIndent > 0.0f);
        foreach (var sortedEntry in Enumerator(values, UseSortSpec ? ImGui.TableGetSortSpecs().Specs : null))
        {
            foreach (var action in ColumnActions)
            {
                ImGui.TableNextColumn();
                action(sortedEntry);
            }

            ImGui.TableNextRow();
        }
    }
}
