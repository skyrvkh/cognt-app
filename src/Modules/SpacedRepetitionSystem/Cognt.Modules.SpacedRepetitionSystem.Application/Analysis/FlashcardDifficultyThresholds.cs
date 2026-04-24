namespace Cognt.Modules.SpacedRepetitionSystem.Application.Analysis;

public static class FlashcardDifficultyThresholds
{
    public const double HighDifficultyMinimum = 8.0d;
    public const int LapseCountMinimum = 2;
    public const int RecentLowRatingMinimum = 2;
    public const int OverdueDaysMinimum = 7;
    public const int RecentRatingWindow = 5;
}
