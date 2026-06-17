using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    private const int ForecastHorizonDays = 90;
    private const int DefaultReviewDurationMilliseconds = 8_000;
    private const int LeechLapseThreshold = 8;

    private static FlashcardWorkloadForecastReadModel CreateWorkloadForecast(
        SqliteFlashcardRepository repository,
        DateOnly today,
        DateTimeOffset todayStartUtc,
        DateTimeOffset tomorrowStartUtc,
        IReadOnlyList<string> deckNames,
        int reviewDurationMilliseconds)
    {
        DateTimeOffset forecastEndUtc = ToReviewTimeZoneOffset(
            today.AddDays(ForecastHorizonDays).ToDateTime(TimeOnly.MinValue)).ToUniversalTime();
        IReadOnlyList<FlashcardScheduledDueRecord> scheduledCards = repository.LoadScheduledDueCards(
            forecastEndUtc,
            deckNames);
        Dictionary<DateOnly, int> dueByDate = new();
        foreach (FlashcardScheduledDueRecord card in scheduledCards)
        {
            DateOnly dueDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(card.DueUtc, ReviewTimeZone).Date);
            if (dueDate < today)
            {
                dueDate = today;
            }

            if (dueDate >= today.AddDays(ForecastHorizonDays))
            {
                continue;
            }

            dueByDate[dueDate] = dueByDate.TryGetValue(dueDate, out int count) ? count + 1 : 1;
        }

        FlashcardWorkloadForecastDayReadModel[] days = Enumerable
            .Range(0, ForecastHorizonDays)
            .Select(offset => CreateForecastDay(today.AddDays(offset), dueByDate, reviewDurationMilliseconds))
            .ToArray();
        int dueIn7Days = SumDueCards(days, 7);
        int dueIn30Days = SumDueCards(days, 30);
        int dueIn90Days = SumDueCards(days, 90);
        int suggestedDailyCapacity = (int)Math.Ceiling(dueIn7Days / 7d);
        int smoothingOpportunityCards = suggestedDailyCapacity == 0
            ? 0
            : days.Take(7).Sum(day => Math.Max(0, day.DueCards - suggestedDailyCapacity));
        DateTimeOffset dayAfterTomorrowStartUtc = ToReviewTimeZoneOffset(
            today.AddDays(2).ToDateTime(TimeOnly.MinValue)).ToUniversalTime();
        FlashcardTomorrowWorkloadContributorReadModel[] contributors = repository
            .LoadTomorrowWorkloadContributors(
                todayStartUtc,
                tomorrowStartUtc,
                dayAfterTomorrowStartUtc,
                deckNames)
            .Select(record => new FlashcardTomorrowWorkloadContributorReadModel(
                record.DeckName,
                record.CardsScheduledToday))
            .ToArray();

        return new FlashcardWorkloadForecastReadModel(
            days,
            dueIn7Days,
            dueIn30Days,
            dueIn90Days,
            suggestedDailyCapacity,
            smoothingOpportunityCards,
            contributors);
    }

    private static FlashcardLearningDiagnosticsReadModel CreateLearningDiagnostics(
        FlashcardLearningDiagnosticsRecord diagnostics,
        IReadOnlyList<FlashcardReviewRatingCountRecord> recallDistribution,
        double desiredRetention)
    {
        int totalReviews = 0;
        int retainedReviews = 0;
        foreach (FlashcardReviewRatingCountRecord rating in recallDistribution)
        {
            totalReviews += rating.Count;
            if (rating.Rating is FlashcardReviewRating.Good or FlashcardReviewRating.Easy)
            {
                retainedReviews += rating.Count;
            }
