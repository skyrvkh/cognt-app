namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsStreakCalculator
{
    private static int CalculateBest(
        DateOnly today,
        DateOnly firstDate,
        IReadOnlyDictionary<DateOnly, int> reviewedByDate)
    {
        int bestStreak = 0;
        int runningStreak = 0;

        for (DateOnly date = firstDate; date <= today; date = date.AddDays(1))
        {
            if (HasReviews(date, reviewedByDate))
            {
                runningStreak++;
                bestStreak = Math.Max(bestStreak, runningStreak);
            }
            else
            {
                runningStreak = 0;
            }
        }

        return bestStreak;
    }
}
