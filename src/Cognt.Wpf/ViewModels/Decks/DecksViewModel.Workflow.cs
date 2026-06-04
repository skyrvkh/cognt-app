using System.IO;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.ViewModels.Decks.PresentationModels;
using CogntApp.ViewModels.Navigation;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels.Decks;

public sealed partial class DecksViewModel
{
    internal void SetCollectionContext(
        string collectionId,
        string? sourceRelPath,
        string? sourceLabel)
    {
        this.collectionId = collectionId;
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
        Cards.RequestSourceFilter(sourceRelPath);
        RefreshFlashcards();
        NavigateToSubview(FlashcardsTab.DecksCards, Decks);
    }

    internal async Task SetCardSuspensionAsync(CardLibraryRow card, bool isSuspended)
    {
        ArgumentNullException.ThrowIfNull(card);

        await SetCardsSuspensionAsync([card], isSuspended);
    }

    internal async Task SetCardsSuspensionAsync(
        IReadOnlyList<CardLibraryRow> cards,
        bool isSuspended)
    {
        ArgumentNullException.ThrowIfNull(cards);

        await RunFlashcardsMutationAsync(
            async () =>
            {
                EnsureCollectionOpen();
                string message = await cardActions.SetCardsSuspensionAsync(collectionId, cards, isSuspended);
                RefreshFlashcards();
                return message;
            },
            failurePrefix: isSuspended ? "Cards could not be suspended" : "Cards could not be unsuspended");
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
                string message = await cardActions.PostponeCardsAsync(collectionId, cards, days);
                RefreshFlashcards();
                return message;
            },
            failurePrefix: "Card could not be postponed");
    }

    internal async Task UpdateCardContentAsync(CardLibraryRow card, CardEditResult edit)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(edit);

        await RunFlashcardsMutationAsync(
            async () =>
            {
                EnsureCollectionOpen();
                string message = await cardActions.UpdateCardContentAsync(collectionId, card, edit);
                RefreshFlashcards();
                return message;
            },
            failurePrefix: "Card could not be updated");
    }

    internal async Task DeleteCardsAsync(IReadOnlyList<CardLibraryRow> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        await RunFlashcardsMutationAsync(
            async () =>
            {
                EnsureCollectionOpen();
                string message = await cardActions.DeleteCardsAsync(collectionId, cards);
                RefreshFlashcards();
                return message;
            },
            failurePrefix: "Cards could not be deleted");
    }


    internal async Task DeleteDecksAsync(IReadOnlyList<DeckRow> decks)
    {
        ArgumentNullException.ThrowIfNull(decks);

        await RunFlashcardsMutationAsync(
            async () =>
            {
                EnsureCollectionOpen();
                string message = await cardActions.DeleteDecksAsync(collectionId, decks);
                RefreshFlashcards();
                return message;
            },
            failurePrefix: "Decks could not be deleted");
    }
    internal async Task AddTagsToCardsAsync(
        IReadOnlyList<CardLibraryRow> cards,
        IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(tags);

        await RunFlashcardsMutationAsync(
            async () =>
            {
                EnsureCollectionOpen();
                string message = await cardActions.AddTagsToCardsAsync(collectionId, cards, tags);
                RefreshFlashcards();
                return message;
            },
            failurePrefix: "Card tags could not be updated");
    }

    internal async Task RemoveTagsFromCardsAsync(
        IReadOnlyList<CardLibraryRow> cards,
        IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(tags);

        await RunFlashcardsMutationAsync(
            async () =>
            {
                EnsureCollectionOpen();
                string message = await cardActions.RemoveTagsFromCardsAsync(collectionId, cards, tags);
                RefreshFlashcards();
                return message;
            },
            failurePrefix: "Card tags could not be updated");
    }

    private void RefreshFlashcards()
    {
        if (string.IsNullOrWhiteSpace(collectionId) || !Directory.Exists(collectionId))
        {
            Decks.LoadDecks([]);
            Cards.LoadFlashcards([], [], [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            Statistics.LoadCollection(null);
            PracticeDueCount = "0";
            CramCardCount = "0";
            return;
        }

        DecksWorkspaceData workspace = workspaceDataProvider.Load(
            collectionId,
            Cards.CreateCardsRequest(collectionId));

        Decks.LoadDecks(workspace.Decks);
        DeckSelectionScope deckScope = CreateDeckSelectionScope(workspace.Decks, Decks.SelectedDeckIds);
        IReadOnlyList<FlashcardCardSummaryReadModel> selectedAllCards = FilterCardsForSelectedDecks(
            workspace.AllCards,
            deckScope.SelectedDeckNameSet);
        IReadOnlyList<FlashcardCardSummaryReadModel> selectedVisibleCards = FilterCardsForSelectedDecks(
            workspace.VisibleCards,
            deckScope.SelectedDeckNameSet);

        Cards.LoadFlashcards(
            selectedVisibleCards,
            deckScope.Decks,
            selectedAllCards,
            workspace.SourceTitlesByRelPath,
            deckScope.HasSelection);
        Statistics.LoadCollection(
            collectionId,
            deckScope.DeckNames);
        PracticeDueCount = workspace.PracticeDueCount;
        CramCardCount = NumericTextFormatter.Format(deckScope.TotalCards);
    }

    private static DeckSelectionScope CreateDeckSelectionScope(
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks,
        IReadOnlySet<long> selectedDeckIds)
    {
        if (selectedDeckIds.Count == 0)
        {
            return new DeckSelectionScope(
                decks,
                [],
                SelectedDeckNameSet: null,
                TotalCards: CountTotalCards(decks),
                HasSelection: false);
        }

        List<FlashcardDeckSummaryReadModel> selectedDecks = new(Math.Min(decks.Count, selectedDeckIds.Count));
        List<string>? selectedDeckNames = null;
        HashSet<string> selectedDeckNameSet = new(Math.Min(decks.Count, selectedDeckIds.Count), StringComparer.OrdinalIgnoreCase);
        int allDeckTotalCards = 0;
        int selectedDeckTotalCards = 0;

        foreach (FlashcardDeckSummaryReadModel deck in decks)
        {
            allDeckTotalCards += deck.TotalCards;
            if (!selectedDeckIds.Contains(deck.Id))
            {
                continue;
            }

            selectedDecks.Add(deck);
            selectedDeckTotalCards += deck.TotalCards;
            if (selectedDeckNameSet.Add(deck.Name))
            {
                selectedDeckNames ??= new(selectedDeckIds.Count);
                selectedDeckNames.Add(deck.Name);
            }
        }

        if (selectedDecks.Count == 0)
        {
            return new DeckSelectionScope(
                decks,
                [],
                SelectedDeckNameSet: null,
                TotalCards: allDeckTotalCards,
                HasSelection: false);
        }

        return new DeckSelectionScope(
            selectedDecks.ToArray(),
            selectedDeckNames?.ToArray() ?? [],
            selectedDeckNameSet,
            selectedDeckTotalCards,
            HasSelection: true);
    }

    private static IReadOnlyList<FlashcardCardSummaryReadModel> FilterCardsForSelectedDecks(
        IReadOnlyList<FlashcardCardSummaryReadModel> cards,
        HashSet<string>? selectedDeckNames)
    {
        if (selectedDeckNames is null)
        {
            return cards;
        }

        List<FlashcardCardSummaryReadModel> filteredCards = new(cards.Count);
        foreach (FlashcardCardSummaryReadModel card in cards)
        {
            if (selectedDeckNames.Contains(card.DeckName))
            {
                filteredCards.Add(card);
            }
        }

        return filteredCards.ToArray();
    }

    private static int CountTotalCards(IReadOnlyList<FlashcardDeckSummaryReadModel> decks)
    {
        int totalCards = 0;
        foreach (FlashcardDeckSummaryReadModel deck in decks)
        {
            totalCards += deck.TotalCards;
        }

        return totalCards;
    }

    private readonly record struct DeckSelectionScope(
        IReadOnlyList<FlashcardDeckSummaryReadModel> Decks,
        string[] DeckNames,
        HashSet<string>? SelectedDeckNameSet,
        int TotalCards,
        bool HasSelection);

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
            logger.LogError(ex, "Flashcards mutation failed. FailurePrefix={FailurePrefix}", failurePrefix);
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

    private IReadOnlyList<string> LoadAvailableTagNames()
    {
        if (string.IsNullOrWhiteSpace(collectionId) || !Directory.Exists(collectionId))
        {
            return [];
        }

        IReadOnlyList<string> tags = noteService.LoadAvailableTags(collectionId);
        return tags;
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

