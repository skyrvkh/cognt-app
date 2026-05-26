using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed partial class CardsSubviewViewModel
{
    private const int MaxSearchFilterSuggestions = 40;
    private readonly ObservableCollection<CardFilterDeckRow> cardFilterDeckRows = [];
    private readonly ObservableCollection<CardFilterOptionRow> cardFilterStatusRows = [];
    private readonly ObservableCollection<CardFilterOptionRow> cardFilterSourceRows = [];
    private readonly ObservableCollection<CardFilterOptionRow> cardFilterTagRows = [];
    private readonly ObservableCollection<CardFilterDeckRow> visibleCardFilterDeckRows = [];
    private readonly ObservableCollection<CardFilterOptionRow> visibleCardFilterSourceRows = [];
    private readonly ObservableCollection<CardFilterOptionRow> visibleCardFilterTagRows = [];
    private readonly HashSet<string> selectedFilterTagSet = new(StringComparer.OrdinalIgnoreCase);
    private string[] selectedFilterTags = [];
    private CardFilterDeckRow? selectedDeckFilter;
    private CardFilterOptionRow? selectedStatusFilter;
    private CardFilterOptionRow? selectedSourceFilter;
    private CardFilterOptionRow? selectedTagFilter;
    private string activeFilterSummary = "No decks";
    private string searchText = string.Empty;
    private string deckFilterSearchText = string.Empty;
    private string sourceFilterSearchText = string.Empty;
    private string tagFilterSearchText = string.Empty;
    private bool isFiltersOpen;
    private bool isUpdatingFilterConfiguration;

    public event EventHandler? FiltersChanged;

    public ICommand OpenFiltersCommand { get; }

    public ICommand CloseFiltersCommand { get; }

    public ICommand ApplyFiltersCommand { get; }

    public ICommand ResetFiltersCommand { get; }

    public IReadOnlyList<CardFilterDeckRow> CardFilterDeckRows => cardFilterDeckRows;

    public IReadOnlyList<CardFilterOptionRow> CardFilterStatusRows => cardFilterStatusRows;

    public IReadOnlyList<CardFilterOptionRow> CardFilterSourceRows => cardFilterSourceRows;

    public IReadOnlyList<CardFilterOptionRow> CardFilterTagRows => cardFilterTagRows;

    public IReadOnlyList<CardFilterOptionRow> VisibleCardFilterSourceRows => visibleCardFilterSourceRows;

    public IReadOnlyList<CardFilterDeckRow> VisibleCardFilterDeckRows => visibleCardFilterDeckRows;

    public IReadOnlyList<CardFilterOptionRow> VisibleCardFilterTagRows => visibleCardFilterTagRows;

    public CardFilterDeckRow? SelectedDeckFilter
    {
        get => selectedDeckFilter;
        set
        {
            if (SetProperty(ref selectedDeckFilter, value) && !isUpdatingFilterConfiguration)
            {
                RefreshVisibleCardFilterDeckRows();
                UpdateActiveFilterSummary();
            }
        }
    }

    public CardFilterOptionRow? SelectedStatusFilter
    {
        get => selectedStatusFilter;
        set
        {
            if (SetProperty(ref selectedStatusFilter, value) && !isUpdatingFilterConfiguration)
            {
                UpdateActiveFilterSummary();
            }
        }
    }

    public CardFilterOptionRow? SelectedSourceFilter
    {
        get => selectedSourceFilter;
        set
        {
            if (SetProperty(ref selectedSourceFilter, value) && !isUpdatingFilterConfiguration)
            {
                RefreshVisibleCardFilterSourceRows();
                UpdateActiveFilterSummary();
            }
        }
    }

    public CardFilterOptionRow? SelectedTagFilter
    {
        get => selectedTagFilter;
        set
        {
            if (SetProperty(ref selectedTagFilter, value) && !isUpdatingFilterConfiguration)
            {
                UpdateActiveFilterSummary();
            }
        }
    }

    public string ActiveFilterSummary
    {
        get => activeFilterSummary;
        private set => SetProperty(ref activeFilterSummary, value);
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            string normalizedValue = value ?? string.Empty;
            if (!SetProperty(ref searchText, normalizedValue))
            {
                return;
            }

            if (isUpdatingFilterConfiguration)
            {
                return;
            }

            UpdateActiveFilterSummary();
            FiltersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string DeckFilterSearchText
    {
        get => deckFilterSearchText;
        set
        {
            string normalizedValue = value ?? string.Empty;
            if (SetProperty(ref deckFilterSearchText, normalizedValue) && !isUpdatingFilterConfiguration)
            {
                RefreshVisibleCardFilterDeckRows();
            }
        }
    }

    public string SourceFilterSearchText
    {
        get => sourceFilterSearchText;
        set
        {
            string normalizedValue = value ?? string.Empty;
            if (SetProperty(ref sourceFilterSearchText, normalizedValue) && !isUpdatingFilterConfiguration)
            {
                RefreshVisibleCardFilterSourceRows();
            }
        }
    }

    public string TagFilterSearchText
    {
        get => tagFilterSearchText;
        set
        {
            string normalizedValue = value ?? string.Empty;
            if (SetProperty(ref tagFilterSearchText, normalizedValue) && !isUpdatingFilterConfiguration)
            {
                RefreshVisibleCardFilterTagRows();
            }
        }
    }

    public bool IsFiltersOpen
    {
        get => isFiltersOpen;
        set
        {
            if (SetProperty(ref isFiltersOpen, value))
            {
                OnPropertyChanged(nameof(FiltersModalVisibility));
                OnPropertyChanged(nameof(FiltersPanelVisibility));
            }
        }
    }

    public Visibility FiltersModalVisibility => IsFiltersOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FiltersPanelVisibility => IsFiltersOpen ? Visibility.Visible : Visibility.Collapsed;

    private void SynchronizeCardFilterDeckRows(IReadOnlyList<CardFilterDeckRow> nextRows)
    {
        SynchronizeRows(
            cardFilterDeckRows,
            nextRows,
            static row => row.Id ?? long.MinValue,
            static (row, next) => row.Update(next.Name, next.Count));

        if (!isUpdatingFilterConfiguration)
        {
            RefreshVisibleCardFilterDeckRows();
        }
    }

    private void SynchronizeCardFilterStatusRows(IReadOnlyList<CardFilterOptionRow> nextRows)
    {
        SynchronizeRows(
            cardFilterStatusRows,
            nextRows,
            static row => row.Value ?? row.Name,
            static (row, next) => row.Update(next.Name, next.Count));
    }

    private void SynchronizeCardFilterSourceRows(IReadOnlyList<CardFilterOptionRow> nextRows)
    {
        SynchronizeRows(
            cardFilterSourceRows,
            nextRows,
            static row => row.Value ?? row.Name,
            static (row, next) => row.Update(next.Name, next.Count));

        if (!isUpdatingFilterConfiguration)
        {
            RefreshVisibleCardFilterSourceRows();
        }
    }

    private void SynchronizeCardFilterTagRows(IReadOnlyList<CardFilterOptionRow> nextRows)
    {
        SynchronizeRows(
            cardFilterTagRows,
            nextRows,
            static row => row.Value ?? row.Name,
            static (row, next) => row.Update(next.Name, next.Count));

        if (!isUpdatingFilterConfiguration)
        {
            RefreshTagFilterPresentationState();
        }
    }

    private void RefreshAllFilterPresentationState()
    {
        RefreshVisibleCardFilterDeckRows();
        RefreshVisibleCardFilterSourceRows();
        RefreshVisibleCardFilterTagRows();
        UpdateActiveFilterSummary();
    }

    private void RefreshTagFilterPresentationState()
    {
        RefreshVisibleCardFilterTagRows();
        UpdateActiveFilterSummary();
    }

    private void RefreshVisibleCardFilterDeckRows()
    {
        SynchronizeVisibleDeckRows(FilterDeckRows(CardFilterDeckRows, DeckFilterSearchText, SelectedDeckFilter));
    }

    private void RefreshVisibleCardFilterSourceRows()
    {
        SynchronizeVisibleOptionRows(
            visibleCardFilterSourceRows,
            FilterOptionRows(
                CardFilterSourceRows,
                SourceFilterSearchText,
                CardsPresentationFactory.AllSourcesValue,
                SelectedSourceFilter));
    }

    private void RefreshVisibleCardFilterTagRows()
    {
        SynchronizeVisibleOptionRows(
            visibleCardFilterTagRows,
            FilterOptionRows(
                CardFilterTagRows,
                TagFilterSearchText,
                CardsPresentationFactory.AllTagsValue));
    }

    private void SynchronizeVisibleDeckRows(IReadOnlyList<CardFilterDeckRow> nextRows)
    {
        SynchronizeRows(visibleCardFilterDeckRows, nextRows);
    }

    private static void SynchronizeVisibleOptionRows(
        ObservableCollection<CardFilterOptionRow> currentRows,
        IReadOnlyList<CardFilterOptionRow> nextRows)
    {
        SynchronizeRows(currentRows, nextRows);
    }

    private static void SynchronizeRows<T>(ObservableCollection<T> currentRows, IReadOnlyList<T> nextRows)
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

        SynchronizeRows(currentRows, synchronizedRows);
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

    private static IReadOnlyList<CardFilterDeckRow> FilterDeckRows(
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

    private static IReadOnlyList<CardFilterOptionRow> FilterOptionRows(
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

    private static bool HasConcreteOptionValue(CardFilterOptionRow row, string allValue)
    {
        return !string.Equals(row.Value, allValue, StringComparison.Ordinal);
    }
}