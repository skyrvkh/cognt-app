using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;
using AppReadModels = Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    private static ReviewQueueCards LoadReviewCardsForPlan(
        string root,
        IReadOnlyList<long> plannedCardIds,
        IReadOnlyDictionary<long, FlashcardRecord> cardsById)
    {
        List<AppReadModels.FlashcardReviewCardReadModel> reviewCards = [];
        List<long> staleCardIds = [];
        foreach (long cardId in plannedCardIds)
        {
            if (!cardsById.TryGetValue(cardId, out FlashcardRecord? card))
            {
                continue;
            }

            if (!TryLoadMaterializedCard(
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
                continue;
            }

            reviewCards.Add(ToReviewCardReadModel(card, materialized));
        }

        return ReviewQueueCards.Create(reviewCards, staleCardIds);
    }

    private static bool TryLoadMaterializedCard(
        string root,
        long cardId,
        string relPath,
        List<long> staleCardIds,
        out MaterializedFlashcardContent materialized)
    {
        try
        {
            string content = FlashcardCollectionStorage.ReadFlashcardFile(root, relPath);
            materialized = FlashcardCardContentCodec.MaterializeSingleFile(content, root);
            return true;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        staleCardIds.Add(cardId);
        materialized = null!;
        return false;
    }

    private static AppReadModels.FlashcardReviewCardReadModel ToReviewCardReadModel(
        FlashcardRecord card,
        MaterializedFlashcardContent materialized)
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
            materialized.BackMultimodalContent);
    }

    private sealed class ReviewQueueCards
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
}
