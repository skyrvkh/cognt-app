using System.Collections.ObjectModel;

namespace CogntApp.ViewModels.Decks.PresentationModels;

internal static class CardFilterPresentationRows
{
    private const int MaxSearchFilterSuggestions = 40;

    internal static void SynchronizeDeckRows(
        ObservableCollection<CardFilterDeckRow> currentRows,
        IReadOnlyList<CardFilterDeckRow> nextRows)
    {
        SynchronizeRows(
            currentRows,
            nextRows,
            static row => row.Id ?? long.MinValue,
            static (row, next) => row.Update(next.Name, next.Count));
    }

    internal static void SynchronizeOptionRows(
        ObservableCollection<CardFilterOptionRow> currentRows,
        IReadOnlyList<CardFilterOptionRow> nextRows)
    {
        SynchronizeRows(
            currentRows,
            nextRows,
            static row => row.Value ?? row.Name,
            static (row, next) => row.Update(next.Name, next.Count));
    }

    internal static void SynchronizeVisibleRows<T>(
        ObservableCollection<T> currentRows,
        IReadOnlyList<T> nextRows)
    {
        int sharedCount = Math.Min(currentRows.Count, nextRows.Count);
        for (int index = 0; index < sharedCount; index++)
        {
            if (!ReferenceEquals(currentRows[index], nextRows[index]))
            {
                currentRows[index] = nextRows[index];
            }
        }

        for (int index = currentRows.Count - 1; index >= nextRows.Count; index--)
        {
            currentRows.RemoveAt(index);
        }

        for (int index = sharedCount; index < nextRows.Count; index++)
        {
            currentRows.Add(nextRows[index]);
        }
    }

    internal static IReadOnlyList<CardFilterDeckRow> FilterDeckRows(
        IReadOnlyList<CardFilterDeckRow> rows,
        string searchText,
        CardFilterDeckRow? selectedRow = null)
    {
        string query = string.IsNullOrWhiteSpace(searchText)
            ? string.Empty
            : searchText.Trim();

        int filteredCapacity = Math.Min(rows.Count, MaxSearchFilterSuggestions);

        List<CardFilterDeckRow>? filteredRows = null;
        foreach (CardFilterDeckRow row in rows)
        {
            if (row.Id.HasValue && (row.IsSelected || ReferenceEquals(row, selectedRow)))
            {
                filteredRows ??= new List<CardFilterDeckRow>(filteredCapacity);
                filteredRows.Add(row);
                if (filteredRows.Count >= MaxSearchFilterSuggestions)
                {
                    return filteredRows;
                }
            }
        }

        if (query.Length == 0)
        {
            return filteredRows ?? [];
        }

        foreach (CardFilterDeckRow row in rows)
        {
            if (row.Id.HasValue
                && !row.IsSelected
                && !ReferenceEquals(row, selectedRow)
                && row.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                filteredRows ??= new List<CardFilterDeckRow>(filteredCapacity);
                filteredRows.Add(row);
                if (filteredRows.Count >= MaxSearchFilterSuggestions)
                {
                    break;
                }
            }
        }

        return filteredRows ?? [];
    }

    internal static IReadOnlyList<CardFilterOptionRow> FilterOptionRows(
        IReadOnlyList<CardFilterOptionRow> rows,
        string searchText,
        string allValue,
        CardFilterOptionRow? selectedRow = null)
    {
        string query = string.IsNullOrWhiteSpace(searchText)
            ? string.Empty
            : searchText.Trim();

        int filteredCapacity = Math.Min(rows.Count, MaxSearchFilterSuggestions);

        List<CardFilterOptionRow>? filteredRows = null;
        foreach (CardFilterOptionRow row in rows)
        {
            if (HasConcreteOptionValue(row, allValue)
                && (row.IsSelected || ReferenceEquals(row, selectedRow)))
            {
                filteredRows ??= new List<CardFilterOptionRow>(filteredCapacity);
                filteredRows.Add(row);
                if (filteredRows.Count >= MaxSearchFilterSuggestions)
                {
                    return filteredRows;
                }
            }
        }

        if (query.Length == 0)
        {
            return filteredRows ?? [];
        }

        foreach (CardFilterOptionRow row in rows)
        {
            if (HasConcreteOptionValue(row, allValue)
                && !row.IsSelected
                && !ReferenceEquals(row, selectedRow)
                && (row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (row.Value?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)))
            {
                filteredRows ??= new List<CardFilterOptionRow>(filteredCapacity);
                filteredRows.Add(row);
                if (filteredRows.Count >= MaxSearchFilterSuggestions)
                {
                    break;
                }
            }
        }

        return filteredRows ?? [];
    }

    private static void SynchronizeRows<TKey, T>(
        ObservableCollection<T> currentRows,
        IReadOnlyList<T> nextRows,
        Func<T, TKey> keySelector,
        Action<T, T> updateAction)
        where TKey : notnull
    {
        Dictionary<TKey, T>? existingByKey = currentRows.Count == 0
            ? null
            : CreateRowLookup(currentRows, keySelector);
        T[] synchronizedRows = new T[nextRows.Count];

        for (int index = 0; index < nextRows.Count; index++)
        {
            T nextRow = nextRows[index];
            if (existingByKey is not null
                && existingByKey.TryGetValue(keySelector(nextRow), out T? existingRow))
            {
                updateAction(existingRow, nextRow);
                synchronizedRows[index] = existingRow;
            }
            else
            {
                synchronizedRows[index] = nextRow;
            }
        }

        SynchronizeVisibleRows(currentRows, synchronizedRows);
    }

    private static Dictionary<TKey, T> CreateRowLookup<TKey, T>(
        IReadOnlyList<T> rows,
        Func<T, TKey> keySelector)
        where TKey : notnull
    {
        Dictionary<TKey, T> lookup = new(rows.Count);
        foreach (T row in rows)
        {
            lookup[keySelector(row)] = row;
        }

        return lookup;
    }

    private static bool HasConcreteOptionValue(CardFilterOptionRow row, string allValue)
    {
        return !string.Equals(row.Value, allValue, StringComparison.Ordinal);
    }
}
