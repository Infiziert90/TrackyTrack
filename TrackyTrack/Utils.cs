using System.ComponentModel;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace TrackyTrack;

public static class Utils
{
    public static string ToStr(SeString content) => content.ToString();
    public static string ToStr(ReadOnlySeString content) => content.ExtractText();

    public record SortedEntry(uint Id, uint Icon, string Name, uint Obtained, uint Min, uint Max, double Percentage);

    public static IEnumerable<SortedEntry> ToSortedEntry(Dictionary<uint, uint> dict, int opened)
    {
        foreach (var (key, value) in dict)
        {
            if (value == 0)
                continue;

            var item = Sheets.GetItem(key);
            yield return new SortedEntry(
                item.RowId,
                CheckItemAction(item),
                ToStr(item.Name),
                value,
                0,
                0,
                (double)value / opened * 100.0);
        }
    }

    public static IEnumerable<SortedEntry> ToSortedEntry(Dictionary<uint, (uint Obtained, List<uint> Amounts)> dict, int opened)
    {
        foreach (var (key, value) in dict)
        {
            if (value.Obtained == 0)
                continue;

            var item = Sheets.GetItem(key);
            yield return new SortedEntry(
                item.RowId,
                CheckItemAction(item),
                ToStr(item.Name),
                value.Obtained,
                value.Amounts.Min(),
                value.Amounts.Max(),
                (double)value.Obtained / opened * 100.0);
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
        return itemAction.Type switch
        {
            1322 => Sheets.MountSheet.GetRow(itemAction.Data[0]).Icon, // Mount ID
            3357 => 87000 + (uint)itemAction.Data[0], // Triple Triad ID
            _ => item.Icon,
        };
    }
}
