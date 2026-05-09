using System.Windows;
using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed partial class CardsSubviewViewModel
{
    private IReadOnlyList<CardFilterDeckRow> cardFilterDeckRows = [];
    private IReadOnlyList<CardFilterOptionRow> cardFilterStatusRows = [];
    private IReadOnlyList<CardFilterOptionRow> cardFilterSourceRows = [];
    private IReadOnlyList<CardFilterOptionRow> cardFilterTagRows = [];
    private CardFilterDeckRow? selectedDeckFilter;
    private CardFilterOptionRow? selectedStatusFilter;
    private CardFilterOptionRow? selectedSourceFilter;
    private CardFilterOptionRow? selectedTagFilter;
    private string activeFilterSummary = "No decks";
    private bool isFiltersOpen;

    public event EventHandler? FiltersChanged;

    public ICommand OpenFiltersCommand { get; }

    public ICommand CloseFiltersCommand { get; }

    public ICommand ApplyFiltersCommand { get; }

    public ICommand ResetFiltersCommand { get; }

    public IReadOnlyList<CardFilterDeckRow> CardFilterDeckRows
    {
        get => cardFilterDeckRows;
        private set => SetProperty(ref cardFilterDeckRows, value);
    }

    public IReadOnlyList<CardFilterOptionRow> CardFilterStatusRows
    {
        get => cardFilterStatusRows;
        private set => SetProperty(ref cardFilterStatusRows, value);
    }

    public IReadOnlyList<CardFilterOptionRow> CardFilterSourceRows
    {
        get => cardFilterSourceRows;
        private set => SetProperty(ref cardFilterSourceRows, value);
    }

    public IReadOnlyList<CardFilterOptionRow> CardFilterTagRows
    {
        get => cardFilterTagRows;
        private set => SetProperty(ref cardFilterTagRows, value);
    }

    public CardFilterDeckRow? SelectedDeckFilter
    {
        get => selectedDeckFilter;
        set
        {
            if (SetProperty(ref selectedDeckFilter, value))
            {
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

    public bool IsFiltersOpen
    {
        get => isFiltersOpen;
        set
        {
            if (SetProperty(ref isFiltersOpen, value))
            {
                OnPropertyChanged(nameof(FiltersModalVisibility));
            }
        }
    }

    public Visibility FiltersModalVisibility => IsFiltersOpen ? Visibility.Visible : Visibility.Collapsed;
}
