using Cognt.Modules.SpacedRepetitionSystem.Domain;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed class FlashcardDashboardSummaryRecord
{
    internal FlashcardDashboardSummaryRecord(
        int totalCards,
        int dueCards,
        int newCards,
        int reviewedTodayCards,
        int difficultCards,
        int reviewHistoryCount)
    {
        TotalCards = totalCards;
        DueCards = dueCards;
        NewCards = newCards;
        ReviewedTodayCards = reviewedTodayCards;
        DifficultCards = difficultCards;
        ReviewHistoryCount = reviewHistoryCount;
    }

    internal int TotalCards { get; }

    internal int DueCards { get; }

    internal int NewCards { get; }

    internal int ReviewedTodayCards { get; }

    internal int DifficultCards { get; }

    internal int ReviewHistoryCount { get; }
}

internal sealed class FlashcardDueCardSummaryRecord
{
    internal FlashcardDueCardSummaryRecord(int dueCards, int dueDecks)
    {
        DueCards = dueCards;
        DueDecks = dueDecks;
    }

    internal int DueCards { get; }

    internal int DueDecks { get; }
}

internal sealed class FlashcardDeckReviewHistorySummaryRecord
{
    internal FlashcardDeckReviewHistorySummaryRecord(
        string deckName,
        int reviewedCount,
        int retainedCount)
    {
        DeckName = deckName;
        ReviewedCount = reviewedCount;
        RetainedCount = retainedCount;
    }

    internal string DeckName { get; }

    internal int ReviewedCount { get; }

    internal int RetainedCount { get; }
}

internal sealed class FlashcardReviewRatingCountRecord
{
    internal FlashcardReviewRatingCountRecord(
        FlashcardReviewRating rating,
        int count)
    {
        Rating = rating;
        Count = count;
    }

    internal FlashcardReviewRating Rating { get; }

    internal int Count { get; }
}

internal sealed class FlashcardWeeklyReviewOutcomeSummaryRecord
{
    internal FlashcardWeeklyReviewOutcomeSummaryRecord(
        int again,
        int hard,
        int good,
        int easy)
    {
        Again = again;
        Hard = hard;
        Good = good;
        Easy = easy;
    }

    internal int Again { get; }

    internal int Hard { get; }

    internal int Good { get; }

    internal int Easy { get; }
}
