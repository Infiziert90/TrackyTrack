using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;

namespace TrackyTrack;

public static class Utils
{
    public static string ToStr(SeString content) => content.ToString();
    public static string ToStr(Lumina.Text.SeString content) => content.ToDalamudString().ToString();

    public record SortedEntry(uint Icon, string Name, uint Count, double Percentage);

    public static IOrderedEnumerable<SortedEntry> SortEntries(IEnumerable<SortedEntry> unsortedList, ImGuiTableColumnSortSpecsPtr sortSpecsPtr)
    {
        object SortFunction(SortedEntry entry) => sortSpecsPtr.ColumnIndex switch
        {
            1 => entry.Name,
            2 => entry.Count,
            3 => entry.Percentage,
            _ => entry.Percentage
        };

        return sortSpecsPtr.SortDirection switch
        {
            ImGuiSortDirection.Ascending => unsortedList.OrderBy(SortFunction),
            ImGuiSortDirection.Descending => unsortedList.OrderByDescending(SortFunction),
            _ => unsortedList.OrderBy(SortFunction)
        };
    }

    public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key) where TValue : new()
    {
        if (!dict.TryGetValue(key, out var val))
        {
            val = new TValue();
            dict.Add(key, val);
        }

        return val;
    }

    public static CharacterConfiguration GetOrCreate(this IDictionary<ulong, CharacterConfiguration> dict, ulong key)
    {
        if (!dict.TryGetValue(key, out var val))
        {
            val = CharacterConfiguration.CreateNew();
            dict.Add(key, val);
        }

        return val;
    }
}
