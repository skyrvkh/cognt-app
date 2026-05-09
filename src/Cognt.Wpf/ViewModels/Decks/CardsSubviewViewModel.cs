using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed partial class CardsSubviewViewModel : ObservableObject
{
    private IReadOnlyList<CardLibraryRow> cardLibraryRows = [];
    private IReadOnlyList<CardEmptyStateRow> cardEmptyStates = [];
    private string showingSummary = "Showing 0 cards";
    private string totalCards = "0";
    private string learningCards = "0";
    private string relearningCards = "0";
    private string dueCards = "0";

    public CardsSubviewViewModel(
        Action showCreateCardReview,
        Action<CardLibraryRow> showEditCard)
    {
        ArgumentNullException.ThrowIfNull(showCreateCardReview);
        ArgumentNullException.ThrowIfNull(showEditCard);

        AddCardCommand = new RelayCommand(showCreateCardReview);
        EditCardCommand = new RelayCommand(parameter =>
        {
            CardLibraryRow? card = parameter switch
            {
                CardLibraryRow row => row,
                _ => null
            };

            if (card is { CanEdit: true })
            {
                showEditCard(card);
            }
        });
        OpenFiltersCommand = new RelayCommand(() => IsFiltersOpen = true);
        CloseFiltersCommand = new RelayCommand(() => IsFiltersOpen = false);
        ApplyFiltersCommand = new RelayCommand(() =>
        {
            FiltersChanged?.Invoke(this, EventArgs.Empty);
            IsFiltersOpen = false;
        });
        ResetFiltersCommand = new RelayCommand(() =>
        {
            ResetFilterSelections();
            FiltersChanged?.Invoke(this, EventArgs.Empty);
            IsFiltersOpen = false;
        });
    }

    public ICommand AddCardCommand { get; }

    public ICommand EditCardCommand { get; }

    public IReadOnlyList<CardLibraryRow> CardLibraryRows
    {
        get => cardLibraryRows;
        private set => SetProperty(ref cardLibraryRows, value);
    }

    public IReadOnlyList<CardEmptyStateRow> CardEmptyStates
    {
        get => cardEmptyStates;
        private set => SetProperty(ref cardEmptyStates, value);
    }

    public string ShowingSummary
    {
        get => showingSummary;
        private set => SetProperty(ref showingSummary, value);
    }

    public string TotalCards
    {
        get => totalCards;
        private set => SetProperty(ref totalCards, value);
    }

    public string LearningCards
    {
        get => learningCards;
        private set => SetProperty(ref learningCards, value);
    }

    public string RelearningCards
    {
        get => relearningCards;
        private set => SetProperty(ref relearningCards, value);
    }

    public string DueCards
    {
        get => dueCards;
        private set => SetProperty(ref dueCards, value);
    }

    public void LoadFlashcards(
        IReadOnlyList<FlashcardCardSummaryReadModel> visibleCards,
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks,
        IReadOnlyList<FlashcardCardSummaryReadModel> filterSourceCards,
        IReadOnlyDictionary<string, string> sourceTitlesByRelPath,
        bool isDeckScopeSelected = false)
    {
        ArgumentNullException.ThrowIfNull(visibleCards);
        ArgumentNullException.ThrowIfNull(decks);
        ArgumentNullException.ThrowIfNull(filterSourceCards);
        ArgumentNullException.ThrowIfNull(sourceTitlesByRelPath);

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;

        CardLibraryRows = CardsPresentationFactory.CreateCardLibraryRows(visibleCards);

        ConfigureFilterOptions(filterSourceCards, decks, nowUtc, sourceTitlesByRelPath);
        ShowingSummary = string.Concat(
            "Showing ",
            NumericTextFormatter.Format(CardLibraryRows.Count),
            " cards");

        int totalCardCount = decks.Sum(deck => deck.TotalCards);
        TotalCards = NumericTextFormatter.Format(totalCardCount);
        LearningCards = NumericTextFormatter.Format(filterSourceCards
            .Count(card => card.State is FlashcardCardStateReadModel.New or FlashcardCardStateReadModel.Learning));
        RelearningCards = NumericTextFormatter.Format(filterSourceCards
            .Count(card => card.State == FlashcardCardStateReadModel.Relearning));
        DueCards = NumericTextFormatter.Format(filterSourceCards
            .Count(card => card.DueUtc <= nowUtc));
        CardEmptyStates = CardsPresentationFactory.CreateEmptyStates(
            totalCardCount,
            CardLibraryRows.Count,
            GetSelectedFilterKind(),
            isDeckScopeSelected,
            decks.Count);
        UpdateActiveFilterSummary();
    }
}
