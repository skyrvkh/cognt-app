using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed partial class CardsSubviewViewModel
{
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
        CardFilterPresentationRows.SynchronizeDeckRows(cardFilterDeckRows, nextRows);

        if (!isUpdatingFilterConfiguration)
        {
            RefreshVisibleCardFilterDeckRows();
        }
    }

    private void SynchronizeCardFilterStatusRows(IReadOnlyList<CardFilterOptionRow> nextRows)
    {
        CardFilterPresentationRows.SynchronizeOptionRows(cardFilterStatusRows, nextRows);
    }

    private void SynchronizeCardFilterSourceRows(IReadOnlyList<CardFilterOptionRow> nextRows)
    {
        CardFilterPresentationRows.SynchronizeOptionRows(cardFilterSourceRows, nextRows);

        if (!isUpdatingFilterConfiguration)
        {
            RefreshVisibleCardFilterSourceRows();
        }
    }

    private void SynchronizeCardFilterTagRows(IReadOnlyList<CardFilterOptionRow> nextRows)
    {
        CardFilterPresentationRows.SynchronizeOptionRows(cardFilterTagRows, nextRows);

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
        CardFilterPresentationRows.SynchronizeVisibleRows(
            visibleCardFilterDeckRows,
            CardFilterPresentationRows.FilterDeckRows(CardFilterDeckRows, DeckFilterSearchText, SelectedDeckFilter));
    }

    private void RefreshVisibleCardFilterSourceRows()
    {
        CardFilterPresentationRows.SynchronizeVisibleRows(
            visibleCardFilterSourceRows,
            CardFilterPresentationRows.FilterOptionRows(
                CardFilterSourceRows,
                SourceFilterSearchText,
                CardsPresentationFactory.AllSourcesValue,
                SelectedSourceFilter));
    }

    private void RefreshVisibleCardFilterTagRows()
    {
        CardFilterPresentationRows.SynchronizeVisibleRows(
            visibleCardFilterTagRows,
            CardFilterPresentationRows.FilterOptionRows(
                CardFilterTagRows,
                TagFilterSearchText,
                CardsPresentationFactory.AllTagsValue));
    }
}
