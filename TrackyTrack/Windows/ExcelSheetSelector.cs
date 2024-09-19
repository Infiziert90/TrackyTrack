﻿using System.Collections;
using Dalamud.Interface.Utility;
using Lumina.Excel;

// From: https://github.com/UnknownX7/Hypostasis/blob/master/ImGui/ExcelSheet.cs
namespace TrackyTrack.Windows
{
    public static class ExcelSheetSelector
    {
        public static ExcelRow[] FilteredSearchSheet = null!;

        private static string SheetSearchText = null!;
        private static string PrevSearchId = null!;
        private static Type PrevSearchType = null!;


        public record ExcelSheetOptions<T> where T : ExcelRow
        {
            public Func<T, string> FormatRow { get; init; } = row => row.ToString();
            public Func<T, string, bool> SearchPredicate { get; init; } = null;
            public Func<T, bool, bool> DrawSelectable { get; init; } = null;
            public IEnumerable<T> FilteredSheet { get; init; } = null;
            public Vector2? Size { get; init; } = null;
        }

        public record ExcelSheetPopupOptions<T> : ExcelSheetOptions<T> where T : ExcelRow
        {
            public ImGuiPopupFlags PopupFlags { get; init; } = ImGuiPopupFlags.None;
            public bool CloseOnSelection { get; init; } = false;
            public Func<T, bool> IsRowSelected { get; init; } = _ => false;
        }

        private static void ExcelSheetSearchInput<T>(string id, IEnumerable<T> filteredSheet, Func<T, string, bool> searchPredicate) where T : ExcelRow
        {
            if (ImGui.IsWindowAppearing() && ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive())
            {
                if (id != PrevSearchId)
                {
                    if (typeof(T) != PrevSearchType)
                    {
                        SheetSearchText = string.Empty;
                        PrevSearchType = typeof(T);
                    }

                    FilteredSearchSheet = null;
                    PrevSearchId = id;
                }

                ImGui.SetKeyboardFocusHere(0);
            }

            if (ImGui.InputTextWithHint("##ExcelSheetSearch", "Search", ref SheetSearchText, 128, ImGuiInputTextFlags.AutoSelectAll))
                FilteredSearchSheet = null;

            FilteredSearchSheet ??= filteredSheet.Where(s => searchPredicate(s, SheetSearchText)).Cast<ExcelRow>().ToArray();
        }

        public static bool ExcelSheetPopup<T>(string id, out uint selectedRow, ExcelSheetPopupOptions<T> options = null, bool close = false) where T : ExcelRow
        {

            options ??= new ExcelSheetPopupOptions<T>();
            var sheet = options.FilteredSheet ?? Plugin.Data.GetExcelSheet<T>();
            selectedRow = 0;
            if (sheet == null)
                return false;

            if (close)
                return false;

            ImGui.SetNextWindowSize(options.Size ?? new Vector2(0, 250 * ImGuiHelpers.GlobalScale));
            if (!ImGui.BeginPopupContextItem(id, options.PopupFlags))
                return false;

            ExcelSheetSearchInput(id, sheet, options.SearchPredicate ?? ((row, s) => options.FormatRow(row).Contains(s, StringComparison.CurrentCultureIgnoreCase)));

            ImGui.BeginChild("ExcelSheetSearchList", Vector2.Zero, true);

            var ret = false;
            var drawSelectable = options.DrawSelectable ?? ((row, selected) => ImGui.Selectable(options.FormatRow(row), selected));
            using (var clipper = new ListClipper(FilteredSearchSheet.Length))
            {
                foreach (var i in clipper.Rows)
                {
                    var row = (T)FilteredSearchSheet[i];

                    ImGui.PushID(id);
                    if (!drawSelectable(row, options.IsRowSelected(row)))
                        continue;
                    selectedRow = row.RowId;
                    ret = true;
                    ImGui.PopID();
                }
            }

            // ImGui issue #273849, children keep popups from closing automatically
            if (ret && options.CloseOnSelection)
                ImGui.CloseCurrentPopup();

            ImGui.EndChild();
            ImGui.EndPopup();
            return ret;
        }
    }

    public unsafe class ListClipper : IEnumerable<(int, int)>, IDisposable
    {
        private ImGuiListClipperPtr Clipper;
        private readonly int rows;
        private readonly int columns;
        private readonly bool TwoDimensional;
        private readonly int ItemRemainder;

        public int FirstRow { get; private set; } = -1;
        public int CurrentRow { get; private set; }
        public int DisplayEnd => Clipper.DisplayEnd;

        public IEnumerable<int> Rows
        {
            get
            {
                while (Clipper.Step()) // Supposedly this calls End()
                {
                    if (Clipper.ItemsHeight > 0 && FirstRow < 0)
                        FirstRow = (int)(ImGui.GetScrollY() / Clipper.ItemsHeight);
                    for (var i = Clipper.DisplayStart; i < Clipper.DisplayEnd; i++)
                    {
                        CurrentRow = i;
                        yield return TwoDimensional ? i : i * columns;
                    }
                }
            }
        }

        public IEnumerable<int> Columns
        {
            get
            {
                var cols = (ItemRemainder == 0 || rows != DisplayEnd || CurrentRow != DisplayEnd - 1) ? columns : ItemRemainder;
                for (var j = 0; j < cols; j++)
                    yield return j;
            }
        }

        public ListClipper(int items, int cols = 1, bool twoD = false, float itemHeight = 0)
        {
            TwoDimensional = twoD;
            columns = cols;
            rows = TwoDimensional ? items : (int)MathF.Ceiling((float)items / columns);
            ItemRemainder = !TwoDimensional ? items % columns : 0;
            Clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
            Clipper.Begin(rows, itemHeight);
        }

        public IEnumerator<(int, int)> GetEnumerator() => (from i in Rows from j in Columns select (i, j)).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
            Clipper.Destroy(); // This also calls End() but I'm calling it anyway just in case
            GC.SuppressFinalize(this);
        }
    }
}
