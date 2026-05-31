using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using AppReadModels = Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal static class ReviewQueueCardLoader
{
    internal static ReviewQueueCards LoadForPlan(
        string root,
        IReadOnlyList<long> plannedCardIds,
        IReadOnlyDictionary<long, FlashcardRecord> cardsById,
        IReadOnlyDictionary<long, AppReadModels.FlashcardReviewCardQueuePlacement>? queuePlacements = null,
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        List<AppReadModels.FlashcardReviewCardReadModel> reviewCards = [];
        List<long> staleCardIds = [];
        List<long> missingMediaCardIds = [];
        int missingPlannedCardCount = 0;
        foreach (long cardId in plannedCardIds)
        {
            if (!cardsById.TryGetValue(cardId, out FlashcardRecord? card))
            {
                missingPlannedCardCount++;
                continue;
            }

            if (!FlashcardCollectionStorage.TryLoadMaterializedCard(
                    root,
                    card.Id,
                    card.FrontRelPath,
                    staleCardIds,
                    out MaterializedFlashcardContent materialized))
            {
                continue;
            }

            if (materialized.Status == FlashcardMaterializationStatus.ReferencedMediaMissing)
            {
                missingMediaCardIds.Add(card.Id);
                continue;
            }

            AppReadModels.FlashcardReviewCardQueuePlacement? queuePlacement = null;
            queuePlacements?.TryGetValue(card.Id, out queuePlacement);
            reviewCards.Add(ToReviewCardReadModel(card, materialized, queuePlacement));
        }

        if (missingMediaCardIds.Count > 0)
        {
            logger?.LogWarning(
                "Review queue skipped cards because referenced media is missing. Root={Root} MissingMediaCardCount={MissingMediaCardCount} MissingMediaCardIds={MissingMediaCardIds}",
                root,
                missingMediaCardIds.Count,
                string.Join(",", missingMediaCardIds.Take(10)));
        }

        if (missingPlannedCardCount > 0)
        {
            logger?.LogWarning(
                "Review queue skipped planned cards that were missing from the repository result. Root={Root} PlannedCardCount={PlannedCardCount} MissingPlannedCardCount={MissingPlannedCardCount}",
                root,
                plannedCardIds.Count,
                missingPlannedCardCount);
        }

        return ReviewQueueCards.Create(reviewCards, staleCardIds);
    }

    private static AppReadModels.FlashcardReviewCardReadModel ToReviewCardReadModel(
        FlashcardRecord card,
        MaterializedFlashcardContent materialized,
        AppReadModels.FlashcardReviewCardQueuePlacement? queuePlacement = null)
    {
        return new AppReadModels.FlashcardReviewCardReadModel(
            card.Id,
            materialized.FrontContent,
            materialized.BackContent,
            materialized.FrontText,
            materialized.BackText,
            materialized.FrontKind,
            materialized.BackKind,
            materialized.Status,
            materialized.FrontMultimodalContent,
            materialized.BackMultimodalContent)
        {
            QueueCategory = queuePlacement?.Category,
            QueueDueUtc = queuePlacement?.DueUtc,
        };
    }
}

internal sealed class ReviewQueueCards
{
    internal ReviewQueueCards(
        IReadOnlyList<AppReadModels.FlashcardReviewCardReadModel> reviewCards,
        IReadOnlyList<long> staleCardIds)
    {
        ReviewCards = reviewCards;
        StaleCardIds = staleCardIds;
    }

    internal IReadOnlyList<AppReadModels.FlashcardReviewCardReadModel> ReviewCards { get; }

    internal IReadOnlyList<long> StaleCardIds { get; }

    internal static ReviewQueueCards Create(
        IReadOnlyList<AppReadModels.FlashcardReviewCardReadModel> reviewCards,
        IReadOnlyList<long> staleCardIds)
    {
        return new ReviewQueueCards(reviewCards, staleCardIds);
    }
}
