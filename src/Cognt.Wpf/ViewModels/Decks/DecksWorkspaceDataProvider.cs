using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels.Decks;

internal sealed class DecksWorkspaceDataProvider : IDecksWorkspaceDataProvider
{
    private readonly IFlashcardDeckSummariesQuery flashcardDeckSummariesQuery;
    private readonly IFlashcardCardsQuery flashcardCardsQuery;
    private readonly INoteService noteService;
    private readonly ILogger<DecksWorkspaceDataProvider>? logger;

    internal DecksWorkspaceDataProvider(
        IFlashcardDeckSummariesQuery flashcardDeckSummariesQuery,
        IFlashcardCardsQuery flashcardCardsQuery,
        INoteService noteService,
        ILogger<DecksWorkspaceDataProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(flashcardDeckSummariesQuery);
        ArgumentNullException.ThrowIfNull(flashcardCardsQuery);
        ArgumentNullException.ThrowIfNull(noteService);

        this.flashcardDeckSummariesQuery = flashcardDeckSummariesQuery;
        this.flashcardCardsQuery = flashcardCardsQuery;
        this.noteService = noteService;
        this.logger = logger;
    }

    DecksWorkspaceData IDecksWorkspaceDataProvider.Load(
        string? collectionId,
        FlashcardCardsQueryRequest filteredRequest)
    {
        ArgumentNullException.ThrowIfNull(filteredRequest);

        if (string.IsNullOrWhiteSpace(collectionId) || !Directory.Exists(collectionId))
        {
            return new DecksWorkspaceData([], [], [], "0", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        var decks = flashcardDeckSummariesQuery.Execute(new FlashcardCollectionQueryRequest(collectionId));
        var allCards = flashcardCardsQuery.Execute(new FlashcardCardsQueryRequest(collectionId));
        IReadOnlyList<Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels.FlashcardCardSummaryReadModel> visibleCards = IsDefaultCardsRequest(filteredRequest)
            ? allCards
            : flashcardCardsQuery.Execute(filteredRequest);
        IReadOnlyDictionary<string, string> sourceTitles = LoadSourceTitles(collectionId);

        return new DecksWorkspaceData(
            decks,
            allCards,
            visibleCards,
            NumericTextFormatter.Format(CountDueCards(allCards, DateTimeOffset.UtcNow)),
            sourceTitles);
    }

    private static int CountDueCards(
        IReadOnlyList<Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels.FlashcardCardSummaryReadModel> cards,
        DateTimeOffset nowUtc)
    {
        int dueCards = 0;
        foreach (var card in cards)
        {
            if (card.DueUtc <= nowUtc)
            {
                dueCards++;
            }
        }

        return dueCards;
    }

    private IReadOnlyDictionary<string, string> LoadSourceTitles(string collectionId)
    {
        try
        {
            Dictionary<string, string> sourceTitles = new(StringComparer.OrdinalIgnoreCase);
            foreach (KnowledgeNoteListItemReadModel note in noteService.LoadNoteCatalog(collectionId))
            {
                if (!string.IsNullOrWhiteSpace(note.RelativePath))
                {
                    sourceTitles.TryAdd(note.RelativePath, note.Title);
                }
            }

            return sourceTitles;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or DirectoryNotFoundException
            or FileNotFoundException
            or PathTooLongException
            or NotSupportedException)
        {
            logger?.LogWarning(
                ex,
                "Deck source title load failed; continuing without note titles. CollectionId={CollectionId}",
                collectionId);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool IsDefaultCardsRequest(FlashcardCardsQueryRequest request)
    {
        return request.DeckId is null
            && request.Filter == FlashcardCardsFilterKind.All
            && string.IsNullOrWhiteSpace(request.SourceRelPath)
            && string.IsNullOrWhiteSpace(request.Tag)
            && string.IsNullOrWhiteSpace(request.SearchText)
            && (request.Tags is null || request.Tags.Count == 0);
    }
}
