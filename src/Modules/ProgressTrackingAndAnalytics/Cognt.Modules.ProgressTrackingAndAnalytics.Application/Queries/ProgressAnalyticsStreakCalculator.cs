namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

internal static partial class ProgressAnalyticsStreakCalculator
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

    private static bool HasReviews(DateOnly date, IReadOnlyDictionary<DateOnly, int> reviewedByDate)
    {
        return reviewedByDate.TryGetValue(date, out int reviewed) && reviewed > 0;
    }
}
