using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application;

internal static partial class FlashcardRequestGuards
{
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
