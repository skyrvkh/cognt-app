namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsStreakCalculator
{
    private static int CalculateCurrent(
        DateOnly today,
        DateOnly firstDate,
        IReadOnlyDictionary<DateOnly, int> reviewedByDate)
    {
        int currentStreak = 0;
        for (DateOnly date = today; date >= firstDate; date = date.AddDays(-1))
        {
            if (!HasReviews(date, reviewedByDate))
            {
                break;
            }

            currentStreak++;
        }

        return currentStreak;
    }
}
