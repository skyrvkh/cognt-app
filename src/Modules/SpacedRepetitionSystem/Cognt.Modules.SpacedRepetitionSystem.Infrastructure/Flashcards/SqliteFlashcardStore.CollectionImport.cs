using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    public Task<ImportFlashcardCollectionResult> ImportCollectionAsync(
        string sourceCollectionId,
        string destinationCollectionId,
        IReadOnlyDictionary<string, string> sourceDocumentPathMap,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCollectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationCollectionId);
        ArgumentNullException.ThrowIfNull(sourceDocumentPathMap);

        cancellationToken.ThrowIfCancellationRequested();

        string sourcePath = FlashcardCollectionStorage.GetExistingCollectionPath(sourceCollectionId);
        string destinationPath = FlashcardCollectionStorage.GetExistingCollectionPath(destinationCollectionId);
        EnsureDistinctImportRoots(sourcePath, destinationPath);

        using SqliteFlashcardRepository sourceRepository =
            FlashcardCollectionStorage.OpenRepositoryForCollectionPath(sourcePath, logger);
        FlashcardCollectionImportSnapshot snapshot = sourceRepository.LoadCollectionImportSnapshot();
        if (snapshot.Cards.Count == 0)
        {
            logger?.LogInformation(
                "Flashcard collection import skipped because the source collection had no importable cards. SourceCollectionId={SourceCollectionId} DestinationCollectionId={DestinationCollectionId}",
                sourceCollectionId,
                destinationCollectionId);
            return Task.FromResult(ImportFlashcardCollectionResult.Empty);
        }

        Dictionary<long, string> deckNamesBySourceId = snapshot.Decks.ToDictionary(deck => deck.Id, deck => deck.Name);
        Dictionary<string, string> normalizedDocumentPathMap = NormalizeDocumentPathMap(sourceDocumentPathMap);
        using SqliteFlashcardRepository destinationRepository =
            FlashcardCollectionStorage.OpenRepositoryForCollectionPath(destinationPath, logger);

        Dictionary<long, long> importedCardIdsBySourceId = [];
        HashSet<string> importedDeckNames = new(StringComparer.OrdinalIgnoreCase);
        int skippedCards = 0;
        int duplicateCards = 0;
        List<long> missingDeckCardIds = [];
        List<long> missingFrontPathCardIds = [];
        List<FlashcardImportCopyFailure> copyFailures = [];
        foreach (FlashcardImportCardRecord sourceCard in snapshot.Cards)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!deckNamesBySourceId.TryGetValue(sourceCard.DeckId, out string? deckName))
            {
                missingDeckCardIds.Add(sourceCard.Id);
                skippedCards++;
                continue;
            }

            if (!TryPrepareFlashcardImportCard(
                    sourcePath,
                    destinationPath,
                    sourceCard,
                    deckName,
                    normalizedDocumentPathMap,
                    missingFrontPathCardIds,
                    copyFailures,
                    logger,
                    out PreparedFlashcardImportCard? preparedCard)
                || preparedCard is null)
            {
                skippedCards++;
                continue;
            }

            long? importedCardId = destinationRepository.TryImportCard(preparedCard);
            if (importedCardId is null)
            {
                DeleteCopiedFlashcardFiles(destinationPath, preparedCard, logger);
                duplicateCards++;
                skippedCards++;
                continue;
            }

            importedCardIdsBySourceId[sourceCard.Id] = importedCardId.Value;
            importedDeckNames.Add(deckName);
        }

        LogSkippedImportCardSummary(
            sourceCollectionId,
            destinationCollectionId,
            missingDeckCardIds,
            missingFrontPathCardIds);
        LogImportCopyFailureSummary(
            sourceCollectionId,
            destinationCollectionId,
            copyFailures);

        int importedHistoryEntries = destinationRepository.ImportReviewHistory(
            importedCardIdsBySourceId,
            snapshot.ReviewHistory);
        int skippedHistoryEntries = Math.Max(0, snapshot.ReviewHistory.Count - importedHistoryEntries);
        if (skippedHistoryEntries > 0)
        {
            logger?.LogWarning(
                "Skipped imported flashcard review history because the referenced cards were not imported. SourceCollectionId={SourceCollectionId} DestinationCollectionId={DestinationCollectionId} ImportedCardCount={ImportedCardCount} DuplicateCardCount={DuplicateCardCount} SkippedCardCount={SkippedCardCount} SkippedHistoryEntryCount={SkippedHistoryEntryCount}",
                sourceCollectionId,
                destinationCollectionId,
                importedCardIdsBySourceId.Count,
                duplicateCards,
                skippedCards,
                skippedHistoryEntries);
        }

        ImportFlashcardCollectionResult result = new(
            importedDeckNames.Count,
            importedCardIdsBySourceId.Count,
            importedHistoryEntries,
            skippedCards);
        logger?.LogInformation(
            "Flashcard collection import completed. SourceCollectionId={SourceCollectionId} DestinationCollectionId={DestinationCollectionId} ImportedDecks={ImportedDecks} ImportedCards={ImportedCards} ImportedReviewHistoryEntries={ImportedReviewHistoryEntries} SkippedCards={SkippedCards} DuplicateCards={DuplicateCards}",
            sourceCollectionId,
            destinationCollectionId,
            result.ImportedDecks,
            result.ImportedCards,
            result.ImportedReviewHistoryEntries,
            result.SkippedCards,
            duplicateCards);
        return Task.FromResult(result);
    }

    private void LogSkippedImportCardSummary(
        string sourceCollectionId,
        string destinationCollectionId,
        IReadOnlyCollection<long> missingDeckCardIds,
        IReadOnlyCollection<long> missingFrontPathCardIds)
    {
        if (missingDeckCardIds.Count == 0 && missingFrontPathCardIds.Count == 0)
        {
            return;
        }

        logger?.LogWarning(
            "Skipped imported flashcards because source records were incomplete. SourceCollectionId={SourceCollectionId} DestinationCollectionId={DestinationCollectionId} MissingDeckCardCount={MissingDeckCardCount} MissingDeckSampleCardIds={MissingDeckSampleCardIds} MissingFrontPathCardCount={MissingFrontPathCardCount} MissingFrontPathSampleCardIds={MissingFrontPathSampleCardIds}",
            sourceCollectionId,
            destinationCollectionId,
            missingDeckCardIds.Count,
            string.Join(",", missingDeckCardIds.Take(10)),
            missingFrontPathCardIds.Count,
            string.Join(",", missingFrontPathCardIds.Take(10)));
    }

    private void LogImportCopyFailureSummary(
        string sourceCollectionId,
        string destinationCollectionId,
        IReadOnlyCollection<FlashcardImportCopyFailure> failures)
    {
        if (failures.Count == 0)
        {
            return;
        }

        logger?.LogWarning(
            "Skipped imported flashcard files because copy failed. SourceCollectionId={SourceCollectionId} DestinationCollectionId={DestinationCollectionId} CopyFailureCount={CopyFailureCount} IoFailureCount={IoFailureCount} UnauthorizedFailureCount={UnauthorizedFailureCount} InvalidPathFailureCount={InvalidPathFailureCount} SampleFailures={SampleFailures}",
            sourceCollectionId,
            destinationCollectionId,
            failures.Count,
            failures.Count(static failure => failure.Kind == FlashcardImportCopyFailureKind.IO),
            failures.Count(static failure => failure.Kind == FlashcardImportCopyFailureKind.Unauthorized),
            failures.Count(static failure => failure.Kind == FlashcardImportCopyFailureKind.InvalidPath),
            string.Join(" | ", failures.Take(5).Select(static failure =>
                $"{failure.Kind}:Card={failure.SourceCardId}:Path={failure.SourceRelPath}:Exception={failure.ExceptionType}")));
    }

    private static bool TryPrepareFlashcardImportCard(
        string sourcePath,
        string destinationPath,
        FlashcardImportCardRecord sourceCard,
        string deckName,
        IReadOnlyDictionary<string, string> sourceDocumentPathMap,
        ICollection<long> missingFrontPathCardIds,
        ICollection<FlashcardImportCopyFailure> copyFailures,
        Microsoft.Extensions.Logging.ILogger? logger,
        out PreparedFlashcardImportCard? preparedCard)
    {
        preparedCard = null;
        if (string.IsNullOrWhiteSpace(sourceCard.FrontRelPath))
        {
            missingFrontPathCardIds.Add(sourceCard.Id);
            return false;
        }

        if (!TryCopyImportedFlashcardFile(
                sourcePath,
                destinationPath,
                sourceCard.FrontRelPath,
                sourceCard.Id,
                copyFailures,
                out string frontRelPath))
        {
            return false;
        }

        string? backRelPath = null;
        if (!string.IsNullOrWhiteSpace(sourceCard.BackRelPath)
            && !TryCopyImportedFlashcardFile(
                sourcePath,
                destinationPath,
                sourceCard.BackRelPath,
                sourceCard.Id,
                copyFailures,
                out backRelPath))
        {
            FlashcardCollectionStorage.DeleteFlashcardFileIfExists(destinationPath, frontRelPath, logger);
            return false;
        }

        string? sourceRelPath = !string.IsNullOrWhiteSpace(sourceCard.SourceRelPath)
            && sourceDocumentPathMap.TryGetValue(
                NormalizeRelativePath(sourceCard.SourceRelPath),
                out string? importedSourceRelPath)
                ? importedSourceRelPath
                : null;
        preparedCard = new PreparedFlashcardImportCard(
            sourceCard,
            deckName,
            frontRelPath,
            backRelPath,
            sourceRelPath,
            sourceRelPath is null ? null : sourceCard.SourceContextId,
            sourceRelPath is null ? null : sourceCard.SourceSlotId,
            sourceRelPath is null ? null : sourceCard.SourceTargetId,
            sourceRelPath is null ? null : sourceCard.SourceSnapshotHash);
        return true;
    }

    private static bool TryCopyImportedFlashcardFile(
        string sourcePath,
        string destinationPath,
        string? sourceRelPath,
        long sourceCardId,
        ICollection<FlashcardImportCopyFailure> copyFailures,
        out string destinationRelPath)
    {
        destinationRelPath = string.Empty;
        if (string.IsNullOrWhiteSpace(sourceRelPath))
        {
            return false;
        }

        try
        {
            destinationRelPath = FlashcardCollectionStorage.CopyFlashcardFile(
                sourcePath,
                sourceRelPath,
                destinationPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            copyFailures.Add(new FlashcardImportCopyFailure(
                ex switch
                {
                    IOException => FlashcardImportCopyFailureKind.IO,
                    UnauthorizedAccessException => FlashcardImportCopyFailureKind.Unauthorized,
                    _ => FlashcardImportCopyFailureKind.InvalidPath,
                },
                sourceCardId,
                sourceRelPath,
                ex.GetType().Name));
            destinationRelPath = string.Empty;
            return false;
        }
    }

    private static Dictionary<string, string> NormalizeDocumentPathMap(
        IReadOnlyDictionary<string, string> sourceDocumentPathMap)
    {
        Dictionary<string, string> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string sourcePath, string destinationPath) in sourceDocumentPathMap)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            {
                continue;
            }

            normalized[NormalizeRelativePath(sourcePath)] = NormalizeRelativePath(destinationPath);
        }

        return normalized;
    }

    private static void DeleteCopiedFlashcardFiles(
        string destinationPath,
        PreparedFlashcardImportCard preparedCard,
        Microsoft.Extensions.Logging.ILogger? logger)
    {
        FlashcardCollectionStorage.DeleteFlashcardFileIfExists(destinationPath, preparedCard.FrontRelPath, logger);
        if (!string.IsNullOrWhiteSpace(preparedCard.BackRelPath))
        {
            FlashcardCollectionStorage.DeleteFlashcardFileIfExists(destinationPath, preparedCard.BackRelPath, logger);
        }
    }

    private static void EnsureDistinctImportRoots(string sourcePath, string destinationPath)
    {
        if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The active collection cannot import itself.");
        }

        if (IsPathInsideRoot(sourcePath, destinationPath) || IsPathInsideRoot(destinationPath, sourcePath))
        {
            throw new InvalidOperationException("Nested collections cannot be imported into each other.");
        }
    }

    private static bool IsPathInsideRoot(string candidatePath, string rootPath)
    {
        string candidate = Path.GetFullPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string root = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return candidate.Length > root.Length
            && candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/').Trim().TrimStart('/');
    }

    private enum FlashcardImportCopyFailureKind
    {
        IO,
        Unauthorized,
        InvalidPath,
    }

    private readonly record struct FlashcardImportCopyFailure(
        FlashcardImportCopyFailureKind Kind,
        long SourceCardId,
        string? SourceRelPath,
        string ExceptionType);
}
