using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public async Task<string> ImportExistingCollectionAsync(
        string sourceCollectionPath,
        CancellationToken cancellationToken = default)
    {
        if (!HasOpenCollection)
        {
            throw new InvalidOperationException("Open an active collection before importing another collection.");
        }

        if (!TryNormalizeExistingCollectionPath(sourceCollectionPath, out string sourceFullPath))
        {
            throw new InvalidOperationException(
                $"Choose a collection folder ending with \"{collectionPathResolver.CollectionDirectoryExtension}\".");
        }

        if (AreSamePath(sourceFullPath, openCollectionPath))
        {
            throw new InvalidOperationException("The active collection cannot import itself.");
        }

        SaveCurrentNoteFromEditor();
        KnowledgeCollectionImportReadModel result = noteService.ImportExistingCollection(
            openCollectionPath,
            sourceFullPath);
        ImportFlashcardCollectionResult flashcards = await importFlashcardCollectionCommand.ExecuteAsync(
            new ImportFlashcardCollectionRequest(
                sourceFullPath,
                openCollectionPath,
                result.ImportedDocumentPathMap),
            cancellationToken);

        RefreshAvailableCollections();
        RefreshNotesTree(preserveExpansion: true);
        RefreshDashboardData();
        ReloadNoteToCardDeckNames();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);

        string message = FormatCollectionImportResult(result, flashcards);
        logger?.LogInformation(
            "Collection import completed. TargetCollectionPath={TargetCollectionPath} SourceCollectionPath={SourceCollectionPath} ImportedDecks={ImportedDecks} ImportedCards={ImportedCards} ImportedReviewHistoryEntries={ImportedReviewHistoryEntries} SkippedCards={SkippedCards}",
            openCollectionPath,
            sourceFullPath,
            flashcards.ImportedDecks,
            flashcards.ImportedCards,
            flashcards.ImportedReviewHistoryEntries,
            flashcards.SkippedCards);
        return message;
    }

    private static string FormatCollectionImportResult(
        KnowledgeCollectionImportReadModel result,
        ImportFlashcardCollectionResult flashcards)
    {
        List<string> parts =
        [
            CountDisplayFormatter.Format(result.ImportedFolders, "folder"),
            CountDisplayFormatter.Format(result.ImportedNotes, "note"),
            CountDisplayFormatter.Format(result.ImportedPdfDocuments, "PDF"),
            CountDisplayFormatter.Format(result.ImportedAttachments, "attachment"),
            CountDisplayFormatter.Format(flashcards.ImportedDecks, "deck"),
            CountDisplayFormatter.Format(flashcards.ImportedCards, "flashcard")
        ];

        if (result.ImportedAttachmentLinks > 0)
        {
            parts.Add(CountDisplayFormatter.Format(result.ImportedAttachmentLinks, "attachment link"));
        }

        if (result.ImportedTaggedItems > 0)
        {
            parts.Add(CountDisplayFormatter.Format(result.ImportedTaggedItems, "tagged item"));
        }

        if (flashcards.ImportedReviewHistoryEntries > 0)
        {
            parts.Add(CountDisplayFormatter.Format(flashcards.ImportedReviewHistoryEntries, "review"));
        }

        string message = $"Imported into \"{result.ImportFolderTitle}\": {string.Join(", ", parts)}.";
        if (result.SkippedMissingFiles > 0)
        {
            message += $"{Environment.NewLine}{CountDisplayFormatter.Format(result.SkippedMissingFiles, "missing file")} skipped.";
        }

        if (flashcards.SkippedCards > 0)
        {
            message += $"{Environment.NewLine}{CountDisplayFormatter.Format(flashcards.SkippedCards, "flashcard")} skipped because card content was missing or a generated card already exists.";
        }

        return message;
    }
}
