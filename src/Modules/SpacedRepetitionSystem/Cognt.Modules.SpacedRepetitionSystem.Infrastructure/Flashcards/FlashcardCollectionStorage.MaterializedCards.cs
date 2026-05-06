using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal static partial class FlashcardCollectionStorage
{
    private static void PruneMissingActiveCardFiles(
        string root,
        SqliteFlashcardRepository repository)
    {
        long[] missingCardIds = repository
            .LoadActiveCardFileReferences()
            .Where(card => !FlashcardFileExists(root, card.FrontRelPath))
            .Select(card => card.Id)
            .ToArray();

        repository.MarkCardsDeleted(missingCardIds);
    }

    private static MaterializedFlashcardContent LoadMaterializedContent(
        string root,
        string frontRelPath)
    {
        string frontFileContent = ReadFlashcardFile(root, frontRelPath);
        return FlashcardCardContentCodec.MaterializeSingleFile(frontFileContent, root);
    }
}
