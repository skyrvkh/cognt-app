using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal FlashcardWeeklyReviewOutcomeSummaryRecord LoadWeeklyReviewOutcomeSummary(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        IReadOnlyList<string> deckNames)
    {
        long start = startUtc.ToUnixTimeSeconds();
        long end = endUtc.ToUnixTimeSeconds();

        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            string deckNameFilter = AddDeckNameFilter(command, deckNames, "c.deck_id");
            command.CommandText = $"""
                SELECT
                    COALESCE(SUM(CASE WHEN h.rating = 1 THEN 1 ELSE 0 END), 0) AS again_count,
                    COALESCE(SUM(CASE WHEN h.rating = 2 THEN 1 ELSE 0 END), 0) AS hard_count,
                    COALESCE(SUM(CASE WHEN h.rating = 3 THEN 1 ELSE 0 END), 0) AS good_count,
                    COALESCE(SUM(CASE WHEN h.rating = 4 THEN 1 ELSE 0 END), 0) AS easy_count
                FROM flashcard_review_history h
                JOIN flashcard_card c ON c.id = h.card_id
                WHERE c.is_deleted = 0
                  AND c.back_rel_path IS NULL
                  AND h.reviewed_at >= $start
                  AND h.reviewed_at < $end
                  {deckNameFilter};
                """;
            command.Parameters.AddWithValue("$start", start);
            command.Parameters.AddWithValue("$end", end);

            using SqliteDataReader reader = command.ExecuteReader();
            reader.Read();
            return new FlashcardWeeklyReviewOutcomeSummaryRecord(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3));
        });
    }

    internal IReadOnlyList<DateTimeOffset> LoadReviewActivity(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        IReadOnlyList<string> deckNames)
    {
        long start = startUtc.ToUnixTimeSeconds();
        long end = endUtc.ToUnixTimeSeconds();

        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            string deckNameFilter = AddDeckNameFilter(command, deckNames, "c.deck_id");
            command.CommandText = $"""
                SELECT h.reviewed_at
                FROM flashcard_review_history h
                JOIN flashcard_card c ON c.id = h.card_id
                WHERE c.is_deleted = 0
                  AND c.back_rel_path IS NULL
                  AND h.reviewed_at >= $start
                  AND h.reviewed_at < $end
                  {deckNameFilter}
                ORDER BY h.reviewed_at;
                """;
            command.Parameters.AddWithValue("$start", start);
            command.Parameters.AddWithValue("$end", end);

            List<DateTimeOffset> activity = [];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                activity.Add(DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)));
            }

            return activity;
        });
    }
}
