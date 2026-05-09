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
