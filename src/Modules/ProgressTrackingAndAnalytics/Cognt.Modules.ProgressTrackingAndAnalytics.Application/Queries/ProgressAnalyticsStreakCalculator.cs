namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static class ProgressAnalyticsStreakCalculator
{
    internal static (int Current, int Best) Calculate(
        DateOnly today,
        IReadOnlyDictionary<DateOnly, int> reviewedByDate)
    {
        DateOnly firstDate = ProgressAnalyticsActivityFactory.GetFirstActivityDate(today, reviewedByDate);

        return (
            CalculateCurrent(today, firstDate, reviewedByDate),
            CalculateBest(today, firstDate, reviewedByDate));
    }

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

    private static bool HasReviews(DateOnly date, IReadOnlyDictionary<DateOnly, int> reviewedByDate)
    {
        return reviewedByDate.TryGetValue(date, out int reviewed) && reviewed > 0;
    }
}
