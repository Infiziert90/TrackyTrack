using System.ComponentModel;
using System.Runtime.CompilerServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;
using Lumina.Excel.Sheets;

namespace TrackyTrack;

public static class Utils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToChance(uint obtained, int total)
        => ToChance((double)obtained, total);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToChance(uint obtained, uint total)
        => ToChance((double)obtained, (int)total);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToChance(double obtained, int total)
        => obtained / total * 100;

    public record SortedEntry(uint Id, uint Icon, string Name, uint Obtained, uint Min, uint Max, double Percentage)
    {
        public static SortedEntry FromItem(Item item, uint obtained, uint min, uint max, double percentage)
            => new(item.RowId, CheckItemAction(item), item.Name.ToString(), obtained, min, max, percentage);
    }

    public static IEnumerable<SortedEntry> ToSortedEntry(Dictionary<uint, uint> dict, int opened)
    {
        foreach (var (key, value) in dict.Where(pair => pair.Value > 0))
            yield return SortedEntry.FromItem(Sheets.GetItem(key), value, 0, 0, ToChance(value, opened));
    }

    public static IEnumerable<SortedEntry> ToSortedEntry(Dictionary<uint, (uint Obtained, List<uint> Amounts)> dict, int opened)
    {
        foreach (var (key, value) in dict)
        {
            if (value.Obtained == 0)
                continue;

            yield return SortedEntry.FromItem(Sheets.GetItem(key), value.Obtained, value.Amounts.Min(), value.Amounts.Max(), ToChance(value.Obtained, opened));
        }
    }

    public static IOrderedEnumerable<SortedEntry> SortEntries(IEnumerable<SortedEntry> unsortedList, object sortSpecsPtr)
    {
        var sortSpec = (ImGuiTableColumnSortSpecsPtr)sortSpecsPtr;
        object SortFunction(SortedEntry entry) => sortSpec.ColumnIndex switch
        {
            1 => entry.Name,
            2 => entry.Obtained,
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
               .AddUiForeground(success, 43)
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

    // From: https://stackoverflow.com/a/1415187
    public static string GetDescription(this Enum value)
    {
        var type = value.GetType();
        var name = Enum.GetName(type, value);
        if (name != null)
        {
            var field = type.GetField(name);
            if (field != null)
                if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attr)
                    return attr.Description;
        }

        return string.Empty;
    }

    /// <summary>
    /// Check Item for resolving ItemAction.
    /// </summary>
    /// <param name="item">The item</param>
    /// <returns>Item icon, or resolved icon</returns>
    public static uint CheckItemAction(Item item)
    {
        if (item.ItemAction.RowId == 0)
            return item.Icon;

        var itemAction = item.ItemAction.Value;
        return itemAction.Action.RowId switch
        {
            1322 => Sheets.MountSheet.GetRow(itemAction.Data[0]).Icon, // Mount ID
            3357 => 87000 + (uint)itemAction.Data[0], // Triple Triad ID
            _ => item.Icon,
        };
    }
}
