using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    internal IReadOnlyList<FlashcardDeckSummaryReadModel> LoadDeckSummaries(string collectionId)
    {
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenPrunedRepositoryForCollectionId(collectionId);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        return repository
            .LoadDeckSummaries(nowUtc)
            .Select(ToDeckSummaryReadModel)
            .ToArray();
    }

    private static FlashcardDeckSummaryReadModel ToDeckSummaryReadModel(
        FlashcardDeckSummaryRecord summary)
    {
        return new FlashcardDeckSummaryReadModel(
            summary.Id,
            summary.Name,
            summary.NewCards,
            summary.LearningCards,
            summary.DueCards,
            summary.TotalCards);
    }
}
