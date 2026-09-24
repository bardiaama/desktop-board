namespace DesktopBoard.Core.Services;

public static class SortOrderHelper
{
    /// <summary>
    /// Re-numbers a list 0..n-1 and returns only the (Id, SortOrder) pairs that changed,
    /// so a reorder writes the minimum number of rows.
    /// </summary>
    public static List<(long Id, int SortOrder)> Reindex<T>(IReadOnlyList<T> items, Func<T, long> id, Func<T, int> current, Action<T, int> apply)
    {
        var changed = new List<(long, int)>();
        for (var i = 0; i < items.Count; i++)
        {
            if (current(items[i]) == i) continue;
            apply(items[i], i);
            changed.Add((id(items[i]), i));
        }
        return changed;
    }
}
