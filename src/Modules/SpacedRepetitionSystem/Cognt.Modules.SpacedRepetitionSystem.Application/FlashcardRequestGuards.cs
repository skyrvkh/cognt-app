using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application;

internal static partial class FlashcardRequestGuards
{
    private static TRequest EnsureRequest<TRequest>(TRequest? request)
        where TRequest : class
    {
        return request ?? throw new ArgumentNullException(nameof(request));
    }

    private static void EnsureCollectionId(string collectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
    }

    private static void EnsureCardCommandTarget(string collectionId, long cardId)
    {
        EnsureCollectionId(collectionId);
        EnsurePersistedCardId(cardId);
    }

    private static void EnsurePersistedCardId(long cardId)
    {
        if (cardId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cardId), cardId, "A persisted card id is required.");
        }
    }

    private static void EnsureReviewRating(FlashcardReviewRatingReadModel rating)
    {
        if (rating is not (FlashcardReviewRatingReadModel.Again
            or FlashcardReviewRatingReadModel.Hard
            or FlashcardReviewRatingReadModel.Good
            or FlashcardReviewRatingReadModel.Easy))
        {
            throw new ArgumentOutOfRangeException(nameof(rating), rating, "A supported review rating is required.");
        }
    }
}
