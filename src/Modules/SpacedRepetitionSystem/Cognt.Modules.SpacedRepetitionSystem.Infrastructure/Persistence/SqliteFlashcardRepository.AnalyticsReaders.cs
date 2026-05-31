using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    private static void AddUnixRangeParameters(
        SqliteCommand command,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        command.Parameters.AddWithValue("$start", startUtc.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$end", endUtc.ToUnixTimeSeconds());
    }

    private static FlashcardDashboardSummaryRecord ReadDashboardSummary(SqliteDataReader reader)
    {
        return new FlashcardDashboardSummaryRecord(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5));
    }

    private static FlashcardDueCardSummaryRecord ReadDueCardSummary(SqliteDataReader reader)
    {
        return new FlashcardDueCardSummaryRecord(
            reader.GetInt32(0),
            reader.GetInt32(1));
    }

    private static FlashcardDeckReviewHistorySummaryRecord ReadDeckReviewHistorySummary(SqliteDataReader reader)
    {
        return new FlashcardDeckReviewHistorySummaryRecord(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetInt32(2));
    }

    private static FlashcardReviewRatingCountRecord ReadReviewRatingCount(SqliteDataReader reader)
    {
        return new FlashcardReviewRatingCountRecord(
            (FlashcardReviewRating)reader.GetInt32(0),
            reader.GetInt32(1));
    }

    private static FlashcardWeeklyReviewOutcomeSummaryRecord ReadWeeklyReviewOutcomeSummary(SqliteDataReader reader)
    {
        return new FlashcardWeeklyReviewOutcomeSummaryRecord(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3));
    }
}
