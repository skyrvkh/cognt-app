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
        }

        int totalLapses = diagnostics.Cards.Sum(card => Math.Max(0, card.Lapses));
        int cardsWithLapses = diagnostics.Cards.Count(card => card.Lapses > 0);
        int leechCards = diagnostics.Cards.Count(card => card.Lapses >= LeechLapseThreshold);
        int? averageReviewDuration = diagnostics.ReviewDurationsMilliseconds.Count == 0
            ? null
            : (int)Math.Round(diagnostics.ReviewDurationsMilliseconds.Average());

        return new FlashcardLearningDiagnosticsReadModel(
            totalReviews == 0 ? null : retainedReviews / (double)totalReviews,
            desiredRetention,
            averageReviewDuration,
            totalLapses,
            cardsWithLapses,
            leechCards,
            CreateStabilityDistribution(diagnostics.Cards),
            CreateDifficultyDistribution(diagnostics.Cards));
    }

    private static FlashcardWorkloadForecastDayReadModel CreateForecastDay(
        DateOnly date,
        IReadOnlyDictionary<DateOnly, int> dueByDate,
        int reviewDurationMilliseconds)
    {
        int dueCards = dueByDate.TryGetValue(date, out int count) ? count : 0;
        double estimatedMinutes = Math.Round(
            dueCards * reviewDurationMilliseconds / 60_000d,
            1,
            MidpointRounding.AwayFromZero);
        return new FlashcardWorkloadForecastDayReadModel(date, dueCards, estimatedMinutes);
    }

    private static int SumDueCards(
        IReadOnlyList<FlashcardWorkloadForecastDayReadModel> days,
        int count)
    {
        return days.Take(Math.Min(count, days.Count)).Sum(day => day.DueCards);
    }

    private static FlashcardDistributionBucketReadModel[] CreateStabilityDistribution(
        IReadOnlyList<FlashcardLearningDiagnosticCardRecord> cards)
    {
        return
        [
            new("Not established", cards.Count(card => !card.Stability.HasValue || card.Stability <= 0d)),
            new("≤ 7 days", cards.Count(card => card.Stability is > 0d and <= 7d)),
            new("8–30 days", cards.Count(card => card.Stability is > 7d and <= 30d)),
            new("31–90 days", cards.Count(card => card.Stability is > 30d and <= 90d)),
            new("> 90 days", cards.Count(card => card.Stability is > 90d)),
        ];
    }

    private static FlashcardDistributionBucketReadModel[] CreateDifficultyDistribution(
        IReadOnlyList<FlashcardLearningDiagnosticCardRecord> cards)
    {
        return
        [
            new("Not established", cards.Count(card => !card.Difficulty.HasValue || card.Difficulty <= 0d)),
            new("Easy (≤ 3)", cards.Count(card => card.Difficulty is > 0d and <= 3d)),
            new("Moderate (3–5)", cards.Count(card => card.Difficulty is > 3d and <= 5d)),
            new("Hard (5–7)", cards.Count(card => card.Difficulty is > 5d and <= 7d)),
            new("Very hard (> 7)", cards.Count(card => card.Difficulty is > 7d)),
        ];
    }

    private static int GetEstimatedReviewDurationMilliseconds(FlashcardLearningDiagnosticsRecord diagnostics)
    {
        return diagnostics.ReviewDurationsMilliseconds.Count == 0
            ? DefaultReviewDurationMilliseconds
            : Math.Clamp(
                (int)Math.Round(diagnostics.ReviewDurationsMilliseconds.Average()),
                1_000,
                60_000);
    }
}
