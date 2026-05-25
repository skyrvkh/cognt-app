using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed partial class CardsSubviewViewModel : ObservableObject
{
    private readonly Func<IReadOnlyList<CardLibraryRow>, int, Task> postponeCards;
    private readonly Func<CardLibraryRow, CardEditResult, Task> updateCardContent;
    private readonly Func<IReadOnlyList<CardLibraryRow>, Task> deleteCards;
    private readonly Func<IReadOnlyList<CardLibraryRow>, IReadOnlyList<string>, Task> addTagsToCards;
    private readonly Func<IReadOnlyList<CardLibraryRow>, IReadOnlyList<string>, Task> removeTagsFromCards;
    private readonly Func<IReadOnlyList<string>> loadAvailableTagNames;
    private readonly Func<EditorView> createEditorView;
    private IReadOnlyList<CardLibraryRow> cardLibraryRows = [];
    private IReadOnlyList<FlashcardDeckSummaryReadModel> deckRows = [];
    private IReadOnlyList<CardEmptyStateRow> cardEmptyStates = [];
    private string showingSummary = "Showing 0 cards";
    private string totalCards = "0";
    private string learningCards = "0";
    private string relearningCards = "0";
    private string dueCards = "0";

    public CardsSubviewViewModel(
        Func<CardLibraryRow, bool, Task> setCardSuspension,
        Func<IReadOnlyList<CardLibraryRow>, int, Task> postponeCards,
        Func<CardLibraryRow, CardEditResult, Task> updateCardContent,
        Func<IReadOnlyList<CardLibraryRow>, Task> deleteCards,
        Func<IReadOnlyList<CardLibraryRow>, IReadOnlyList<string>, Task> addTagsToCards,
        Func<IReadOnlyList<CardLibraryRow>, IReadOnlyList<string>, Task> removeTagsFromCards,
        Func<IReadOnlyList<string>> loadAvailableTagNames,
        Func<EditorView> createEditorView)
    {
        ArgumentNullException.ThrowIfNull(setCardSuspension);
        ArgumentNullException.ThrowIfNull(postponeCards);
        ArgumentNullException.ThrowIfNull(updateCardContent);
        ArgumentNullException.ThrowIfNull(deleteCards);
        ArgumentNullException.ThrowIfNull(addTagsToCards);
        ArgumentNullException.ThrowIfNull(removeTagsFromCards);
        ArgumentNullException.ThrowIfNull(loadAvailableTagNames);
        ArgumentNullException.ThrowIfNull(createEditorView);

        this.postponeCards = postponeCards;
        this.updateCardContent = updateCardContent;
        this.deleteCards = deleteCards;
        this.addTagsToCards = addTagsToCards;
        this.removeTagsFromCards = removeTagsFromCards;
        this.loadAvailableTagNames = loadAvailableTagNames;
        this.createEditorView = createEditorView;

        ToggleSuspensionCommand = new RelayCommand(async parameter =>
        {
            if (parameter is CardLibraryRow card && card.CanSchedule)
            {
                await setCardSuspension(card, !card.IsSuspended);
            }
        });
        PostponeOneDayCommand = new RelayCommand(async parameter => await PostponeAsync(parameter, 1, postponeCards));
        PostponeThreeDaysCommand = new RelayCommand(async parameter => await PostponeAsync(parameter, 3, postponeCards));
        PostponeSevenDaysCommand = new RelayCommand(async parameter => await PostponeAsync(parameter, 7, postponeCards));
        PostponeFourteenDaysCommand = new RelayCommand(async parameter => await PostponeAsync(parameter, 14, postponeCards));
        PostponeThirtyDaysCommand = new RelayCommand(async parameter => await PostponeAsync(parameter, 30, postponeCards));
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

    public ICommand ToggleSuspensionCommand { get; }

    public ICommand PostponeOneDayCommand { get; }

    public ICommand PostponeThreeDaysCommand { get; }

    public ICommand PostponeSevenDaysCommand { get; }

    public ICommand PostponeFourteenDaysCommand { get; }

    public ICommand PostponeThirtyDaysCommand { get; }

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
        deckRows = decks;

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

    internal Task PostponeCardsAsync(IReadOnlyList<CardLibraryRow> cards, int days)
    {
        ArgumentNullException.ThrowIfNull(cards);

        CardLibraryRow[] schedulableCards = cards
            .Where(card => card.CanSchedule)
            .GroupBy(card => card.Id)
            .Select(group => group.First())
            .ToArray();
        return schedulableCards.Length == 0
            ? Task.CompletedTask
            : postponeCards(schedulableCards, days);
    }

    internal Task DeleteCardsAsync(IReadOnlyList<CardLibraryRow> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        CardLibraryRow[] distinctCards = cards
            .Where(card => card.Id > 0)
            .GroupBy(card => card.Id)
            .Select(group => group.First())
            .ToArray();
        return distinctCards.Length == 0
            ? Task.CompletedTask
            : deleteCards(distinctCards);
    }

    internal Task UpdateCardContentAsync(CardLibraryRow card, CardEditResult edit)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(edit);

        return card.CanEdit
            ? updateCardContent(card, edit)
            : Task.CompletedTask;
    }

    internal EditorView CreateEditorView()
    {
        return createEditorView();
    }

    internal IReadOnlyList<string> GetDeckNames()
    {
        return deckRows
            .Select(deck => deck.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal Task AddTagsToCardsAsync(
        IReadOnlyList<CardLibraryRow> cards,
        IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(tags);

        CardLibraryRow[] distinctCards = cards
            .Where(card => card.Id > 0)
            .GroupBy(card => card.Id)
            .Select(group => group.First())
            .ToArray();
        return distinctCards.Length == 0 || tags.Count == 0
            ? Task.CompletedTask
            : addTagsToCards(distinctCards, tags);
    }

    internal Task RemoveTagsFromCardsAsync(
        IReadOnlyList<CardLibraryRow> cards,
        IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(tags);

        CardLibraryRow[] distinctCards = cards
            .Where(card => card.Id > 0)
            .GroupBy(card => card.Id)
            .Select(group => group.First())
            .ToArray();
        return distinctCards.Length == 0 || tags.Count == 0
            ? Task.CompletedTask
            : removeTagsFromCards(distinctCards, tags);
    }

    internal IReadOnlyList<string> GetAvailableTagNames()
    {
        return loadAvailableTagNames()
            .Concat(CardFilterTagRows
                .Where(row => !string.Equals(row.Value, CardsPresentationFactory.AllTagsValue, StringComparison.Ordinal))
                .Select(row => row.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Task PostponeAsync(
        object? parameter,
        int days,
        Func<IReadOnlyList<CardLibraryRow>, int, Task> postponeCards)
    {
        return parameter is CardLibraryRow card && card.CanSchedule
            ? postponeCards([card], days)
            : Task.CompletedTask;
    }
}
