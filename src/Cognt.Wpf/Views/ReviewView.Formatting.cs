using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;

namespace CogntApp.Views;

public partial class ReviewView
{
    private static string GetLimitSummaryText(FlashcardReviewQueueSummaryReadModel summary)
    {
        if (summary.ReviewLimitExcluded > 0 && summary.NewLimitExcluded > 0)
        {
            return $"Review limited {summary.ReviewLimitExcluded} | New limited {summary.NewLimitExcluded}";
        }

        if (summary.ReviewLimitExcluded > 0)
        {
            return $"Review limited {summary.ReviewLimitExcluded}";
        }

        return $"New limited {summary.NewLimitExcluded}";
    }

    private static string FormatNextDue(DateTimeOffset dueUtc)
    {
        DateTimeOffset localDue = dueUtc.ToLocalTime();
        DateTimeOffset localNow = DateTimeOffset.Now;
        return localDue.Date == localNow.Date
            ? localDue.ToString("h:mm tt")
            : localDue.ToString("MMM d h:mm tt");
    }

    private static string FormatCompletionSummary(CramCompletionSummary summary)
    {
        return string.Join(
            Environment.NewLine,
            $"Cards selected: {summary.CardsSelected:N0}",
            $"Cards mastered: {summary.CardsMastered:N0}",
            $"Cards skipped/invalid: {summary.CardsSkippedInvalid:N0}",
            $"Total attempts: {summary.TotalAttempts:N0}",
            $"Again: {summary.AgainRatings:N0}  Hard: {summary.HardRatings:N0}  Good: {summary.GoodRatings:N0}  Easy: {summary.EasyRatings:N0}",
            $"Session duration: {FormatDuration(summary.SessionDuration)}",
            $"Required pass count: {summary.RequiredPassCount:N0}",
            $"Counting mode: {summary.PassCountingMode}");
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
    }
}
