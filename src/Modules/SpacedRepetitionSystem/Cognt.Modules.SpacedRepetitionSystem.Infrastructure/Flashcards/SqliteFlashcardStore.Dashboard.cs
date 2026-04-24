using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    internal FlashcardDashboardSummaryReadModel LoadDashboardSummary(
        string collectionId,
        IReadOnlyList<string>? deckNames)
    {
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenPrunedRepositoryForCollectionId(collectionId);

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        var (todayStartUtc, tomorrowStartUtc) = GetLocalTodayUtcBounds(nowUtc);
        FlashcardDashboardSummaryRecord summary = repository.LoadDashboardSummary(
            nowUtc,
            todayStartUtc,
            tomorrowStartUtc,
            deckNames ?? []);
        IReadOnlyList<FlashcardReviewActivityReadModel> reviewActivity = LoadDashboardReviewActivity(
            repository,
            nowUtc,
            deckNames ?? []);

        return ToDashboardSummaryReadModel(summary, reviewActivity);
    }

    internal FlashcardProgressAnalyticsReadModel LoadProgressAnalytics(
        string collectionId,
        DateOnly today,
        IReadOnlyList<string>? deckNames)
    {
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenPrunedRepositoryForCollectionId(collectionId);
        IReadOnlyList<string> selectedDeckNames = deckNames ?? [];

        DateTimeOffset dueTomorrowStartUtc = ToReviewTimeZoneOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue)).ToUniversalTime();
        DateTimeOffset dayAfterTomorrowStartUtc = ToReviewTimeZoneOffset(today.AddDays(2).ToDateTime(TimeOnly.MinValue)).ToUniversalTime();
        FlashcardDueCardSummaryRecord dueTomorrow = repository.LoadDueCardSummary(
            dueTomorrowStartUtc,
            dayAfterTomorrowStartUtc,
            selectedDeckNames);

        IReadOnlyList<FlashcardDeckReviewHistorySummaryRecord> deckReviewHistory = repository.LoadDeckReviewHistorySummaries(selectedDeckNames);
        IReadOnlyList<FlashcardReviewRatingCountRecord> recallDistribution = repository.LoadReviewRatingCounts(selectedDeckNames);

        return new FlashcardProgressAnalyticsReadModel(
            dueTomorrow.DueCards,
            dueTomorrow.DueDecks,
            deckReviewHistory
                .Select(summary => new FlashcardDeckReviewHistoryReadModel(
                    summary.DeckName,
                    summary.ReviewedCount,
                    summary.RetainedCount))
                .ToArray(),
            recallDistribution
                .Select(count => new FlashcardReviewRatingCountReadModel(
                    (Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels.FlashcardReviewRatingReadModel)count.Rating,
                    count.Count))
                .ToArray(),
            LoadWeeklyReviewOutcomes(repository, today, selectedDeckNames));
    }
}
