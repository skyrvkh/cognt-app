using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

namespace CogntApp.ViewModels.Decks;

internal sealed class DecksWorkspaceDataProvider : IDecksWorkspaceDataProvider
{
    private readonly IFlashcardDeckSummariesQuery flashcardDeckSummariesQuery;
    private readonly IFlashcardCardsQuery flashcardCardsQuery;
    private readonly INoteService noteService;

    internal DecksWorkspaceDataProvider(
        IFlashcardDeckSummariesQuery flashcardDeckSummariesQuery,
        IFlashcardCardsQuery flashcardCardsQuery,
        INoteService noteService)
    {
        ArgumentNullException.ThrowIfNull(flashcardDeckSummariesQuery);
        ArgumentNullException.ThrowIfNull(flashcardCardsQuery);
        ArgumentNullException.ThrowIfNull(noteService);

        this.flashcardDeckSummariesQuery = flashcardDeckSummariesQuery;
        this.flashcardCardsQuery = flashcardCardsQuery;
        this.noteService = noteService;
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
            NumericTextFormatter.Format(allCards.Count(card => card.DueUtc <= DateTimeOffset.UtcNow)),
            sourceTitles);
    }

    private IReadOnlyDictionary<string, string> LoadSourceTitles(string collectionId)
    {
        try
        {
            return noteService.LoadNoteCatalog(collectionId)
                .Where(note => !string.IsNullOrWhiteSpace(note.RelativePath))
                .GroupBy(note => note.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Title,
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or DirectoryNotFoundException
            or FileNotFoundException
            or PathTooLongException
            or NotSupportedException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool IsDefaultCardsRequest(FlashcardCardsQueryRequest request)
    {
        return request.DeckId is null
            && request.Filter == FlashcardCardsFilterKind.All
            && string.IsNullOrWhiteSpace(request.SourceRelPath)
            && string.IsNullOrWhiteSpace(request.Tag)
            && (request.Tags is null || request.Tags.Count == 0);
    }
}
