namespace Cognt.Modules.SpacedRepetitionSystem.Domain;

public enum FlashcardLearningState
{
    New = 0,
    Learning = 1,
    Review = 2,
    Relearning = 3,
}

public enum FlashcardReviewRating
{
    Again = 1,
    Hard = 2,
    Good = 3,
    Easy = 4,
}

public sealed record FlashcardSchedule(
    string SerializedState,
    string? ReviewLog,
    DateTimeOffset DueUtc,
    FlashcardLearningState State,
    double? Stability,
    double? Difficulty,
    int? Step,
    DateTimeOffset? LastReviewUtc,
    int ElapsedDays,
    int ScheduledDays,
    int Reps,
    int Lapses);
