namespace Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

public enum FlashcardCardStateReadModel
{
    New = 0,
    Learning = 1,
    Review = 2,
    Relearning = 3,
}

public enum FlashcardReviewRatingReadModel
{
    Again = 1,
    Hard = 2,
    Good = 3,
    Easy = 4,
}
