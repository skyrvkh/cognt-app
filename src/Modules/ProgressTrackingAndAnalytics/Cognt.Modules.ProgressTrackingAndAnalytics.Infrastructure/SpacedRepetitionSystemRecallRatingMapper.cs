using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal static class SpacedRepetitionSystemRecallRatingMapper
{
    internal static ProgressAnalyticsRecallRating Map(FlashcardReviewRatingReadModel rating)
    {
        return rating switch
        {
            FlashcardReviewRatingReadModel.Again => ProgressAnalyticsRecallRating.Again,
            FlashcardReviewRatingReadModel.Hard => ProgressAnalyticsRecallRating.Hard,
            FlashcardReviewRatingReadModel.Good => ProgressAnalyticsRecallRating.Good,
            FlashcardReviewRatingReadModel.Easy => ProgressAnalyticsRecallRating.Easy,
            _ => ProgressAnalyticsRecallRating.Good
        };
    }
}
