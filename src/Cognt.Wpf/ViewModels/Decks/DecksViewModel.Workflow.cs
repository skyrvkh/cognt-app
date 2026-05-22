using System.IO;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels.Navigation;
using CogntApp.ViewModels.Decks.PresentationModels;

namespace CogntApp.ViewModels.Decks;

public sealed partial class DecksViewModel
{
    internal void SetCollectionContext(
        string collectionId,
        string? sourceRelPath,
        string? sourceLabel)
    {
        this.collectionId = collectionId;
        this.sourceRelPath = sourceRelPath;
        CreateCardReview.SetSource(sourceLabel);
        RefreshFlashcards();
    }

    internal void ShowCardsForSource(
        string collectionId,
        string sourceRelPath,
        string sourceLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLabel);

        this.collectionId = string.IsNullOrWhiteSpace(collectionId) ? string.Empty : collectionId;
        this.sourceRelPath = sourceRelPath;
        Cards.RequestSourceFilter(sourceRelPath);
        CreateCardReview.SetSource(sourceLabel);
        RefreshFlashcards();
        NavigateToSubview(FlashcardsTab.DecksCards, Decks);
    }

    internal async Task<int> CreateCardsAsync(
        string deckName,
        string content,
        IReadOnlyList<string> tags)
    {
        EnsureCollectionOpen();

        int created = await createFlashcardsCommand.ExecuteAsync(
            new CreateFlashcardsRequest(
                collectionId,
                string.IsNullOrWhiteSpace(deckName) ? "Default" : deckName.Trim(),
                content,
                tags,
                sourceRelPath));

        RefreshFlashcards();
        SetActiveTab(FlashcardsTab.DecksCards);
        return created;
    }

    internal async Task HandleCreateCardsRequestedAsync(CreateCardsRequestedEventArgs request)
    {
        ArgumentNullException.ThrowIfNull(request);

        await RunFlashcardsMutationAsync(
            async () =>
            {
                int created = await CreateCardsAsync(request.DeckName, request.Content, request.Tags);
                return $"Created {created} card{(created == 1 ? string.Empty : "s")}.";
            },
            failurePrefix: "Cards could not be created");
    }

    internal async Task UpdateCardFrontAsync(long cardId, string frontContent)
    {
        EnsureCollectionOpen();

        await updateFlashcardFrontContentCommand.ExecuteAsync(
            new UpdateFlashcardFrontContentRequest(collectionId, cardId, frontContent));

        RefreshFlashcards();
        SetActiveTab(FlashcardsTab.DecksCards);
    }

    internal async Task SetCardSuspensionAsync(CardLibraryRow card, bool isSuspended)
    {
        ArgumentNullException.ThrowIfNull(card);

        await RunFlashcardsMutationAsync(
            async () =>
            {
                EnsureCollectionOpen();
                await updateFlashcardSchedulingCommand.SetSuspensionAsync(
                    new UpdateFlashcardSuspensionRequest(collectionId, card.Id, isSuspended));
                RefreshFlashcards();
                return isSuspended ? "Card suspended." : "Card unsuspended.";
            },
            failurePrefix: isSuspended ? "Card could not be suspended" : "Card could not be unsuspended");
    }

    internal async Task PostponeCardAsync(CardLibraryRow card, int days)
    {
        ArgumentNullException.ThrowIfNull(card);

        await PostponeCardsAsync([card], days);
    }

    internal async Task PostponeCardsAsync(IReadOnlyList<CardLibraryRow> cards, int days)
    {
        ArgumentNullException.ThrowIfNull(cards);

        await RunFlashcardsMutationAsync(
            async () =>
            {
                EnsureCollectionOpen();
                CardLibraryRow[] distinctCards = cards
                    .Where(card => card.CanSchedule)
                    .GroupBy(card => card.Id)
                    .Select(group => group.First())
                    .ToArray();
                foreach (CardLibraryRow card in distinctCards)
                {
                    await updateFlashcardSchedulingCommand.PostponeDueDateAsync(
                        new PostponeFlashcardDueDateRequest(collectionId, card.Id, days));
                }

                RefreshFlashcards();
                string cardLabel = distinctCards.Length == 1 ? "Card" : $"{distinctCards.Length} cards";
                return $"{cardLabel} postponed by {days} day{(days == 1 ? string.Empty : "s")}.";
            },
            failurePrefix: "Card could not be postponed");
    }

    internal async Task HandleSaveCardRequestedAsync(SaveCardRequestedEventArgs request)
    {
        ArgumentNullException.ThrowIfNull(request);

        await RunFlashcardsMutationAsync(
            async () =>
            {
                await UpdateCardFrontAsync(request.CardId, request.Content);
                return "Card saved.";
            },
            failurePrefix: "Card could not be saved");
    }

    private void RefreshFlashcards()
    {
        if (string.IsNullOrWhiteSpace(collectionId) || !Directory.Exists(collectionId))
        {
            Decks.LoadDecks([]);
            Cards.LoadFlashcards([], [], [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            Statistics.LoadCollection(null);
            CreateCardReview.LoadDecks([]);
            PracticeDueCount = "0";
            CramCardCount = "0";
            return;
        }

        DecksWorkspaceData workspace = workspaceDataProvider.Load(
            collectionId,
            Cards.CreateCardsRequest(collectionId, includeDeckFilter: false));

        Decks.LoadDecks(workspace.Decks);
        IReadOnlyList<FlashcardDeckSummaryReadModel> selectedDecks = GetSelectedDecks(workspace.Decks, Decks.SelectedDeckIds);
        IReadOnlyList<FlashcardCardSummaryReadModel> selectedAllCards = FilterCardsForSelectedDecks(
            workspace.AllCards,
            selectedDecks);
        IReadOnlyList<FlashcardCardSummaryReadModel> selectedVisibleCards = FilterCardsForSelectedDecks(
            workspace.VisibleCards,
            selectedDecks);
        IReadOnlyList<FlashcardDeckSummaryReadModel> cardsScopeDecks = selectedDecks.Count == 0
            ? workspace.Decks
            : selectedDecks;

        Cards.LoadFlashcards(
            selectedVisibleCards,
            cardsScopeDecks,
            selectedAllCards,
            workspace.SourceTitlesByRelPath,
            selectedDecks.Count > 0);
        Statistics.LoadCollection(
            collectionId,
            selectedDecks.Select(deck => deck.Name).ToArray());
        PracticeDueCount = workspace.PracticeDueCount;
        CramCardCount = NumericTextFormatter.Format(cardsScopeDecks.Sum(deck => deck.TotalCards));
        CreateCardReview.LoadDecks(workspace.Decks.Select(deck => deck.Name));
    }

    private static IReadOnlyList<FlashcardDeckSummaryReadModel> GetSelectedDecks(
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks,
        IReadOnlyCollection<long> selectedDeckIds)
    {
        if (selectedDeckIds.Count == 0)
        {
            return [];
        }

        return decks
            .Where(deck => selectedDeckIds.Contains(deck.Id))
            .ToArray();
    }

    private static IReadOnlyList<FlashcardCardSummaryReadModel> FilterCardsForSelectedDecks(
        IReadOnlyList<FlashcardCardSummaryReadModel> cards,
        IReadOnlyList<FlashcardDeckSummaryReadModel> selectedDecks)
    {
        if (selectedDecks.Count == 0)
        {
            return cards;
        }

        HashSet<string> selectedDeckNames = selectedDecks
            .Select(deck => deck.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return cards
            .Where(card => selectedDeckNames.Contains(card.DeckName))
            .ToArray();
    }

    private async Task RunFlashcardsMutationAsync(
        Func<Task<string>> mutation,
        string failurePrefix)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentException.ThrowIfNullOrWhiteSpace(failurePrefix);

        try
        {
            string successMessage = await mutation();
            RaiseInfoMessage("Flashcards", successMessage);
        }
        catch (Exception ex)
        {
            RaiseWarningMessage("Flashcards", $"{failurePrefix}: {ex.Message}");
        }
    }

    private void EnsureCollectionOpen()
    {
        if (string.IsNullOrWhiteSpace(collectionId) || !Directory.Exists(collectionId))
        {
            throw new InvalidOperationException("Open a collection before creating flashcards.");
        }
    }

    private void RaiseInfoMessage(string title, string message)
    {
        InfoMessageRequested?.Invoke(this, new UserMessageRequestedEventArgs(title, message));
    }

    private void RaiseWarningMessage(string title, string message)
    {
        WarningMessageRequested?.Invoke(this, new UserMessageRequestedEventArgs(title, message));
    }
}
