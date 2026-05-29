namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

public interface IImportFlashcardCollectionCommand
{
    Task<ImportFlashcardCollectionResult> ExecuteAsync(
        ImportFlashcardCollectionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ImportFlashcardCollectionRequest(
    string SourceCollectionId,
    string DestinationCollectionId,
    IReadOnlyDictionary<string, string> SourceDocumentPathMap);

public sealed record ImportFlashcardCollectionResult(
    int ImportedDecks,
    int ImportedCards,
    int ImportedReviewHistoryEntries,
    int SkippedCards)
{
    public static ImportFlashcardCollectionResult Empty { get; } = new(
        ImportedDecks: 0,
        ImportedCards: 0,
        ImportedReviewHistoryEntries: 0,
        SkippedCards: 0);
}
