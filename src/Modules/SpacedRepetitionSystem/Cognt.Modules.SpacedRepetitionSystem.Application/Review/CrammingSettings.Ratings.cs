using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Review;

public sealed partial record CrammingSettings
{
    public bool HasAcceptedRating => CountHardAsPass || CountGoodAsPass || CountEasyAsPass;

    public bool IsAcceptedRating(FlashcardReviewRatingReadModel rating)
    {
        return rating switch
        {
            FlashcardReviewRatingReadModel.Hard => CountHardAsPass,
            FlashcardReviewRatingReadModel.Good => CountGoodAsPass,
            FlashcardReviewRatingReadModel.Easy => CountEasyAsPass,
            _ => false,
        };
    }
}
