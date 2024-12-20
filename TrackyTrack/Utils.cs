using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Utility;
using Lumina.Text.ReadOnly;

namespace TrackyTrack;

public static class Utils
{
    public static string ToStr(SeString content) => content.ToString();
    public static string ToStr(ReadOnlySeString content) => content.ToDalamudString().ToString();

    public record SortedEntry(uint Id, uint Icon, string Name, uint Count, double Percentage);

    public static IOrderedEnumerable<SortedEntry> SortEntries(IEnumerable<SortedEntry> unsortedList, object sortSpecsPtr)
    {
        var sortSpec = (ImGuiTableColumnSortSpecsPtr)sortSpecsPtr;
        object SortFunction(SortedEntry entry) => sortSpec.ColumnIndex switch
        {
            1 => entry.Name,
            2 => entry.Count,
            3 => entry.Percentage,
            _ => entry.Percentage
        };

        return sortSpec.SortDirection switch
        {
            ImGuiSortDirection.Ascending => unsortedList.OrderBy(SortFunction),
            ImGuiSortDirection.Descending => unsortedList.OrderByDescending(SortFunction),
            _ => unsortedList.OrderBy(SortFunction)
        };
    }

    public static SeString SuccessMessage(string success)
    {
        return new SeStringBuilder()
               .AddUiForeground("[Tracky] ", 540)
               .AddUiForeground($"{success}", 43)
               .BuiltString;
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

    public static uint NormalizeItemId(uint itemId)
    {
        return itemId > 1_000_000 ? itemId - 1_000_000 : itemId > 500_000 ? itemId - 500_000 : itemId;
    }

    public static void AddNotification(string content, NotificationType type)
    {
        Plugin.NotificationManager.AddNotification(new Notification{Content = content, Type = type, Minimized = false});
    }

    public static bool NeedsRefresh(ref long lastRefresh, int refreshRate)
    {
        if (Environment.TickCount64 < lastRefresh)
            return false;

        lastRefresh = Environment.TickCount64 + refreshRate;
        return true;
    }
}
