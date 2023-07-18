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
        Func<SortedEntry, object> sortFunc = sortSpecsPtr.ColumnIndex switch
        {
            1 => x => x.Name,
            2 => x => x.Count,
            3 => x => x.Percentage,
            _ => x => x.Percentage
        };

        return sortSpecsPtr.SortDirection switch
        {
            ImGuiSortDirection.Ascending => unsortedList.OrderBy(sortFunc),
            ImGuiSortDirection.Descending => unsortedList.OrderByDescending(sortFunc),
            _ => unsortedList.OrderBy(sortFunc)
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
