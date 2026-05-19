using System.ComponentModel;
using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed class DecksSubviewViewModel : ObservableObject
{
    private IReadOnlyList<DeckRow> deckRows = [];
    private string deckCountSummary = "0";
    private string decksShowingSummary = "Showing 0 decks";
    private string selectedDeckSummary = "All decks";
    private bool suppressDeckSelectionChanged;

    public DecksSubviewViewModel(
        Action showCreateCardReview,
        Action showStatistics,
        CardsSubviewViewModel cards,
        StatisticsSubviewViewModel statistics)
    {
        ArgumentNullException.ThrowIfNull(showCreateCardReview);
        ArgumentNullException.ThrowIfNull(showStatistics);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(statistics);

        Cards = cards;
        Statistics = statistics;
        AddCardCommand = new RelayCommand(showCreateCardReview);
        ShowStatisticsCommand = new RelayCommand(showStatistics);
        SelectAllDecksCommand = new RelayCommand(() => SetAllDeckSelections(true));
        ClearDeckSelectionCommand = new RelayCommand(() => SetAllDeckSelections(false));
    }

    public event EventHandler? SelectedDecksChanged;

    public ICommand AddCardCommand { get; }

    public ICommand ShowStatisticsCommand { get; }

    public ICommand SelectAllDecksCommand { get; }

    public ICommand ClearDeckSelectionCommand { get; }

    public CardsSubviewViewModel Cards { get; }

    public StatisticsSubviewViewModel Statistics { get; }

    public IReadOnlyList<DeckRow> DeckRows
    {
        get => deckRows;
        private set => SetProperty(ref deckRows, value);
    }

    public string DeckCountSummary
    {
        get => deckCountSummary;
        private set => SetProperty(ref deckCountSummary, value);
    }

    public string DecksShowingSummary
    {
        get => decksShowingSummary;
        private set => SetProperty(ref decksShowingSummary, value);
    }

    public string SelectedDeckSummary
    {
        get => selectedDeckSummary;
        private set => SetProperty(ref selectedDeckSummary, value);
    }

    public bool HasDecks => DeckRows.Count > 0;

    public bool HasSelectedDecks => DeckRows.Any(deck => deck.IsSelected);

    public bool ShouldShowNoDecksState => !HasDecks;

    internal IReadOnlyCollection<long> SelectedDeckIds => DeckRows
        .Where(deck => deck.IsSelected)
        .Select(deck => deck.Id)
        .ToArray();

    public void LoadDecks(IReadOnlyList<FlashcardDeckSummaryReadModel> decks)
    {
        ArgumentNullException.ThrowIfNull(decks);

        HashSet<long> selectedDeckIds = SelectedDeckIds.ToHashSet();
        UnsubscribeDeckRows();

        suppressDeckSelectionChanged = true;
        DeckRows = decks
            .Select(deck => ToDeckRow(deck, selectedDeckIds.Contains(deck.Id)))
            .ToArray();
        SubscribeDeckRows();
        suppressDeckSelectionChanged = false;

        UpdateDeckSummaries(decks.Count, decks.Count);
        UpdateSelectedDeckSummary();
        NotifyDeckStateChanged();
    }

    private void SetAllDeckSelections(bool isSelected)
    {
        if (DeckRows.Count == 0)
        {
            return;
        }

        suppressDeckSelectionChanged = true;
        foreach (DeckRow deck in DeckRows)
        {
            deck.IsSelected = isSelected;
        }

        suppressDeckSelectionChanged = false;
        OnDeckSelectionChanged();
    }

    private void SubscribeDeckRows()
    {
        foreach (DeckRow deck in DeckRows)
        {
            deck.PropertyChanged += DeckRow_PropertyChanged;
        }
    }

    private void UnsubscribeDeckRows()
    {
        foreach (DeckRow deck in DeckRows)
        {
            deck.PropertyChanged -= DeckRow_PropertyChanged;
        }
    }

    private void DeckRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (suppressDeckSelectionChanged
            || !string.Equals(e.PropertyName, nameof(DeckRow.IsSelected), StringComparison.Ordinal))
        {
            return;
        }

        OnDeckSelectionChanged();
    }

    private void OnDeckSelectionChanged()
    {
        UpdateSelectedDeckSummary();
        OnPropertyChanged(nameof(HasSelectedDecks));
        SelectedDecksChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSelectedDeckSummary()
    {
        IReadOnlyList<DeckRow> selectedDecks = DeckRows
            .Where(deck => deck.IsSelected)
            .ToArray();

        SelectedDeckSummary = selectedDecks.Count switch
        {
            0 => "All decks",
            1 => selectedDecks[0].Name,
            _ => string.Concat(NumericTextFormatter.Format(selectedDecks.Count), " decks selected")
        };
    }

    private static DeckRow ToDeckRow(FlashcardDeckSummaryReadModel deck, bool isSelected)
    {
        return new DeckRow(
            deck.Id,
            deck.Name,
            "Deck",
            deck.NewCards + deck.LearningCards,
            deck.DueCards,
            deck.TotalCards,
            deck.ProgressPercent,
            isSelected);
    }

    private void UpdateDeckSummaries(
        int visibleDecks,
        int totalDecks)
    {
        DeckCountSummary = NumericTextFormatter.Format(totalDecks);
        DecksShowingSummary = totalDecks == 1
            ? "Showing 1 deck"
            : string.Concat("Showing ", NumericTextFormatter.Format(visibleDecks), " of ", NumericTextFormatter.Format(totalDecks), " decks");
    }

    private void NotifyDeckStateChanged()
    {
        OnPropertyChanged(nameof(HasDecks));
        OnPropertyChanged(nameof(HasSelectedDecks));
        OnPropertyChanged(nameof(ShouldShowNoDecksState));
    }
}
