using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    private static FlashcardDashboardSummaryReadModel ToDashboardSummaryReadModel(
        FlashcardDashboardSummaryRecord summary,
        IReadOnlyList<FlashcardReviewActivityReadModel> reviewActivity)
    {
        return new FlashcardDashboardSummaryReadModel(
            summary.TotalCards,
            summary.DueCards,
            summary.NewCards,
            summary.ReviewedTodayCards,
            summary.DifficultCards,
            summary.ReviewHistoryCount,
            reviewActivity);
    }

    private static IReadOnlyList<FlashcardReviewActivityReadModel> LoadDashboardReviewActivity(
        SqliteFlashcardRepository repository,
        DateTimeOffset nowUtc,
        IReadOnlyList<string> deckNames)
    {
        var (startUtc, endUtc) = GetReviewActivityWindowUtc(nowUtc);

        return repository
            .LoadReviewActivity(startUtc, endUtc, deckNames)
            .GroupBy(reviewedAtUtc => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                reviewedAtUtc,
                ReviewTimeZone).Date))
            .Select(ToReviewActivityReadModel)
            .OrderBy(activity => activity.Date)
            .ToArray();
    }

    private static IReadOnlyList<FlashcardWeeklyReviewOutcomeReadModel> LoadWeeklyReviewOutcomes(
        SqliteFlashcardRepository repository,
        DateOnly today,
        IReadOnlyList<string> deckNames)
    {
        const int weeklyOutcomeWindowCount = 5;
        const int daysPerWeek = 7;

        DateOnly currentWeekStart = GetWeekStart(today);
        DateOnly firstWeekStart = currentWeekStart.AddDays(-(weeklyOutcomeWindowCount - 1) * daysPerWeek);

        return Enumerable
            .Range(0, weeklyOutcomeWindowCount)
            .Select(index =>
            {
                DateOnly weekStart = firstWeekStart.AddDays(index * daysPerWeek);
                DateOnly weekEnd = weekStart.AddDays(daysPerWeek - 1);
                FlashcardWeeklyReviewOutcomeSummaryRecord summary = repository.LoadWeeklyReviewOutcomeSummary(
                    ToReviewTimeZoneOffset(weekStart.ToDateTime(TimeOnly.MinValue)).ToUniversalTime(),
                    ToReviewTimeZoneOffset(weekEnd.AddDays(1).ToDateTime(TimeOnly.MinValue)).ToUniversalTime(),
                    deckNames);

                return new FlashcardWeeklyReviewOutcomeReadModel(
                    weekStart,
                    weekEnd,
                    summary.Again,
                    summary.Hard,
                    summary.Good,
                    summary.Easy);
            })
            .ToArray();
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetReviewActivityWindowUtc(
        DateTimeOffset nowUtc)
    {
        const int weekCount = 52;
        const int daysPerWeek = 7;
        DateTimeOffset nowLocal = TimeZoneInfo.ConvertTime(nowUtc, ReviewTimeZone);
        DateOnly today = DateOnly.FromDateTime(nowLocal.Date);
        DateOnly firstDate = GetWeekStart(today).AddDays(-(weekCount - 1) * daysPerWeek);
        DateTimeOffset startLocal = ToReviewTimeZoneOffset(firstDate.ToDateTime(TimeOnly.MinValue));
        DateTimeOffset endLocal = ToReviewTimeZoneOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue));

        return (startLocal.ToUniversalTime(), endLocal.ToUniversalTime());
    }

    private static FlashcardReviewActivityReadModel ToReviewActivityReadModel(
        IGrouping<DateOnly, DateTimeOffset> group)
    {
        return new FlashcardReviewActivityReadModel(group.Key, group.Count());
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        const int daysPerWeek = 7;
        int dayOffset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + daysPerWeek) % daysPerWeek;
        return date.AddDays(-dayOffset);
    }
}
