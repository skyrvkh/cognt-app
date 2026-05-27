using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed class DecksSubviewViewModel : ObservableObject
{
    private readonly Dictionary<long, string> selectedDeckNamesById = [];
    private readonly ObservableCollection<DeckRow> deckRows = [];
    private HashSet<long> selectedDeckIds = [];
    private string deckCountSummary = "0";
    private string decksShowingSummary = "Showing 0 decks";
    private string selectedDeckSummary = "All decks";
    private bool suppressDeckSelectionChanged;

    public DecksSubviewViewModel(
        Action showStatistics,
        CardsSubviewViewModel cards,
        StatisticsSubviewViewModel statistics)
    {
        ArgumentNullException.ThrowIfNull(showStatistics);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(statistics);

        Cards = cards;
        Statistics = statistics;
        ShowStatisticsCommand = new RelayCommand(showStatistics);
        SelectAllDecksCommand = new RelayCommand(() => SetAllDeckSelections(true));
        ClearDeckSelectionCommand = new RelayCommand(() => SetAllDeckSelections(false));
    }

    public event EventHandler? SelectedDecksChanged;

    public ICommand ShowStatisticsCommand { get; }

    public ICommand SelectAllDecksCommand { get; }

    public ICommand ClearDeckSelectionCommand { get; }

    public CardsSubviewViewModel Cards { get; }

    public StatisticsSubviewViewModel Statistics { get; }

    public IReadOnlyList<DeckRow> DeckRows => deckRows;

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

    public bool HasSelectedDecks => selectedDeckIds.Count > 0;

    public bool ShouldShowNoDecksState => !HasDecks;

    internal IReadOnlySet<long> SelectedDeckIds => selectedDeckIds;

    public void LoadDecks(IReadOnlyList<FlashcardDeckSummaryReadModel> decks)
    {
        ArgumentNullException.ThrowIfNull(decks);

        IReadOnlySet<long> currentSelectedDeckIds = SelectedDeckIds;
        UnsubscribeDeckRows();

        suppressDeckSelectionChanged = true;
        try
        {
            DeckLoadState loadState = CreateDeckLoadState(decks, currentSelectedDeckIds, deckRows);
            SynchronizeDeckRows(loadState.Rows);
            ApplySelectedDeckState(loadState.SelectionState);
        }
        finally
        {
            SubscribeDeckRows();
            suppressDeckSelectionChanged = false;
        }

        UpdateDeckSummaries(deckRows.Count, decks.Count);
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
        ApplySelectedDeckState(CreateSelectedDeckState(DeckRows));
        OnPropertyChanged(nameof(HasSelectedDecks));
        SelectedDecksChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SynchronizeDeckRows(IReadOnlyList<DeckRow> nextRows)
    {
        int sharedCount = Math.Min(deckRows.Count, nextRows.Count);
        for (int index = 0; index < sharedCount; index++)
        {
            if (!ReferenceEquals(deckRows[index], nextRows[index]))
            {
                deckRows[index] = nextRows[index];
            }
        }

        for (int index = deckRows.Count - 1; index >= nextRows.Count; index--)
        {
            deckRows.RemoveAt(index);
        }

        for (int index = sharedCount; index < nextRows.Count; index++)
        {
            deckRows.Add(nextRows[index]);
        }
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
            || !string.Equals(e.PropertyName, nameof(DeckRow.IsSelected), StringComparison.Ordinal)
            || sender is not DeckRow row)
        {
            return;
        }

        OnDeckSelectionChanged(row);
    }

    private void OnDeckSelectionChanged(DeckRow row)
    {
        if (row.IsSelected)
        {
            selectedDeckIds.Add(row.Id);
            selectedDeckNamesById[row.Id] = row.Name;
        }
        else
        {
            selectedDeckIds.Remove(row.Id);
            selectedDeckNamesById.Remove(row.Id);
        }

        SelectedDeckSummary = CreateSelectedDeckSummary(selectedDeckNamesById);
        OnPropertyChanged(nameof(HasSelectedDecks));
        SelectedDecksChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplySelectedDeckState(DeckSelectionState state)
    {
        selectedDeckIds = state.Ids;
        selectedDeckNamesById.Clear();
        foreach ((long id, string name) in state.NamesById)
        {
            selectedDeckNamesById.Add(id, name);
        }

        SelectedDeckSummary = state.Summary;
    }

    private static DeckLoadState CreateDeckLoadState(
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks,
        IReadOnlySet<long> selectedDeckIds,
        IReadOnlyList<DeckRow> existingRows)
    {
        DeckRow[] rows = new DeckRow[decks.Count];
        Dictionary<long, DeckRow>? existingRowsById = existingRows.Count == 0
            ? null
            : CreateDeckRowLookup(existingRows);
        Dictionary<long, string>? selectedNamesById = null;
        HashSet<long>? selectedIds = null;

        for (int index = 0; index < decks.Count; index++)
        {
            FlashcardDeckSummaryReadModel deck = decks[index];
            bool isSelected = selectedDeckIds.Contains(deck.Id);
            DeckRow row = existingRowsById is not null
                && existingRowsById.TryGetValue(deck.Id, out DeckRow? existingRow)
                ? UpdateDeckRow(existingRow, deck, isSelected)
                : ToDeckRow(deck, isSelected);
            rows[index] = row;

            if (!isSelected)
            {
                continue;
            }

            selectedNamesById ??= new(selectedDeckIds.Count);
            selectedIds ??= new(selectedDeckIds.Count);
            selectedNamesById[row.Id] = row.Name;
            selectedIds.Add(row.Id);
        }

        return new DeckLoadState(
            rows,
            CreateDeckSelectionState(selectedNamesById, selectedIds));
    }

    private static DeckSelectionState CreateSelectedDeckState(IReadOnlyList<DeckRow> decks)
    {
        Dictionary<long, string>? selectedNamesById = null;
        HashSet<long>? selectedIds = null;
        foreach (DeckRow deck in decks)
        {
            if (!deck.IsSelected)
            {
                continue;
            }

            selectedNamesById ??= [];
            selectedIds ??= [];
            selectedNamesById[deck.Id] = deck.Name;
            selectedIds.Add(deck.Id);
        }

        return CreateDeckSelectionState(selectedNamesById, selectedIds);
    }

    private static DeckSelectionState CreateDeckSelectionState(
        Dictionary<long, string>? selectedNamesById,
        HashSet<long>? selectedIds)
    {
        Dictionary<long, string> namesById = selectedNamesById ?? [];
        HashSet<long> ids = selectedIds ?? [];
        return new DeckSelectionState(
            ids,
            namesById,
            CreateSelectedDeckSummary(namesById));
    }

    private static string CreateSelectedDeckSummary(IReadOnlyDictionary<long, string> selectedNamesById)
    {
        return selectedNamesById.Count switch
        {
            0 => "All decks",
            1 => GetOnlySelectedDeckName(selectedNamesById),
            _ => string.Concat(NumericTextFormatter.Format(selectedNamesById.Count), " decks selected")
        };
    }

    private static string GetOnlySelectedDeckName(IReadOnlyDictionary<long, string> selectedNamesById)
    {
        foreach (string name in selectedNamesById.Values)
        {
            return name;
        }

        return "All decks";
    }

    private static Dictionary<long, DeckRow> CreateDeckRowLookup(IReadOnlyList<DeckRow> rows)
    {
        Dictionary<long, DeckRow> lookup = new(rows.Count);
        foreach (DeckRow row in rows)
        {
            lookup[row.Id] = row;
        }

        return lookup;
    }

    private static DeckRow UpdateDeckRow(DeckRow row, FlashcardDeckSummaryReadModel deck, bool isSelected)
    {
        row.Update(
            deck.Name,
            "Deck",
            deck.NewCards + deck.LearningCards,
            deck.DueCards,
            deck.TotalCards,
            deck.ProgressPercent);
        row.IsSelected = isSelected;
        return row;
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

    private readonly record struct DeckLoadState(
        DeckRow[] Rows,
        DeckSelectionState SelectionState);

    private readonly record struct DeckSelectionState(
        HashSet<long> Ids,
        Dictionary<long, string> NamesById,
        string Summary);
}