using System.Windows;
using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed partial class CardsSubviewViewModel
{
    private const int MaxSearchFilterSuggestions = 40;
    private IReadOnlyList<CardFilterDeckRow> cardFilterDeckRows = [];
    private IReadOnlyList<CardFilterOptionRow> cardFilterStatusRows = [];
    private IReadOnlyList<CardFilterOptionRow> cardFilterSourceRows = [];
    private IReadOnlyList<CardFilterOptionRow> cardFilterTagRows = [];
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

    public event EventHandler? FiltersChanged;

    public ICommand OpenFiltersCommand { get; }

    public ICommand CloseFiltersCommand { get; }

    public ICommand ApplyFiltersCommand { get; }

    public ICommand ResetFiltersCommand { get; }

    public IReadOnlyList<CardFilterDeckRow> CardFilterDeckRows
    {
        get => cardFilterDeckRows;
        private set
        {
            if (SetProperty(ref cardFilterDeckRows, value))
            {
                OnPropertyChanged(nameof(VisibleCardFilterDeckRows));
            }
        }
    }

    public IReadOnlyList<CardFilterOptionRow> CardFilterStatusRows
    {
        get => cardFilterStatusRows;
        private set => SetProperty(ref cardFilterStatusRows, value);
    }

    public IReadOnlyList<CardFilterOptionRow> CardFilterSourceRows
    {
        get => cardFilterSourceRows;
        private set
        {
            if (SetProperty(ref cardFilterSourceRows, value))
            {
                OnPropertyChanged(nameof(VisibleCardFilterSourceRows));
            }
        }
    }

    public IReadOnlyList<CardFilterOptionRow> CardFilterTagRows
    {
        get => cardFilterTagRows;
        private set
        {
            if (SetProperty(ref cardFilterTagRows, value))
            {
                OnPropertyChanged(nameof(VisibleCardFilterTagRows));
            }
        }
    }

    public IReadOnlyList<CardFilterOptionRow> VisibleCardFilterSourceRows =>
        FilterOptionRows(
            CardFilterSourceRows,
            SourceFilterSearchText,
            CardsPresentationFactory.AllSourcesValue,
            SelectedSourceFilter);

    public IReadOnlyList<CardFilterDeckRow> VisibleCardFilterDeckRows =>
        FilterDeckRows(CardFilterDeckRows, DeckFilterSearchText, SelectedDeckFilter);

    public IReadOnlyList<CardFilterOptionRow> VisibleCardFilterTagRows =>
        FilterOptionRows(CardFilterTagRows, TagFilterSearchText, CardsPresentationFactory.AllTagsValue);

    public CardFilterDeckRow? SelectedDeckFilter
    {
        get => selectedDeckFilter;
        set
        {
            if (SetProperty(ref selectedDeckFilter, value))
            {
                OnPropertyChanged(nameof(VisibleCardFilterDeckRows));
                UpdateActiveFilterSummary();
            }
        }
    }

    public CardFilterOptionRow? SelectedStatusFilter
    {
        get => selectedStatusFilter;
        set
        {
            if (SetProperty(ref selectedStatusFilter, value))
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
            if (SetProperty(ref selectedSourceFilter, value))
            {
                OnPropertyChanged(nameof(VisibleCardFilterSourceRows));
                UpdateActiveFilterSummary();
            }
        }
    }

    public CardFilterOptionRow? SelectedTagFilter
    {
        get => selectedTagFilter;
        set
        {
            if (SetProperty(ref selectedTagFilter, value))
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
            if (SetProperty(ref deckFilterSearchText, normalizedValue))
            {
                OnPropertyChanged(nameof(VisibleCardFilterDeckRows));
            }
        }
    }

    public string SourceFilterSearchText
    {
        get => sourceFilterSearchText;
        set
        {
            string normalizedValue = value ?? string.Empty;
            if (SetProperty(ref sourceFilterSearchText, normalizedValue))
            {
                OnPropertyChanged(nameof(VisibleCardFilterSourceRows));
            }
        }
    }

    public string TagFilterSearchText
    {
        get => tagFilterSearchText;
        set
        {
            string normalizedValue = value ?? string.Empty;
            if (SetProperty(ref tagFilterSearchText, normalizedValue))
            {
                OnPropertyChanged(nameof(VisibleCardFilterTagRows));
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

    private static IReadOnlyList<CardFilterDeckRow> FilterDeckRows(
        IReadOnlyList<CardFilterDeckRow> rows,
        string searchText,
        CardFilterDeckRow? selectedRow = null)
    {
        string query = string.IsNullOrWhiteSpace(searchText)
            ? string.Empty
            : searchText.Trim();
        bool HasConcreteValue(CardFilterDeckRow row) => row.Id.HasValue;

        return rows
            .Where(row => HasConcreteValue(row) && (row.IsSelected || ReferenceEquals(row, selectedRow)))
            .Concat(rows.Where(row =>
                query.Length > 0
                && HasConcreteValue(row)
                && !row.IsSelected
                && !ReferenceEquals(row, selectedRow)
                && row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Take(MaxSearchFilterSuggestions)
            .ToArray();
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
        bool HasConcreteValue(CardFilterOptionRow row) =>
            !string.Equals(row.Value, allValue, StringComparison.Ordinal);

        return rows
            .Where(row => HasConcreteValue(row) && (row.IsSelected || ReferenceEquals(row, selectedRow)))
            .Concat(rows.Where(row =>
                query.Length > 0
                && HasConcreteValue(row)
                && !row.IsSelected
                && !ReferenceEquals(row, selectedRow)
                && (row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (row.Value?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))))
            .Take(MaxSearchFilterSuggestions)
            .ToArray();
    }
}
