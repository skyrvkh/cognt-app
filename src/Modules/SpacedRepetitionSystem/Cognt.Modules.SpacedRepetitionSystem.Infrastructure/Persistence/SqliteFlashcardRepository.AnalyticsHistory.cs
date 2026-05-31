using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal IReadOnlyList<FlashcardDeckReviewHistorySummaryRecord> LoadDeckReviewHistorySummaries(
        IReadOnlyList<string> deckNames)
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            string deckNameFilter = AddDeckNameFilter(command, deckNames, "d.id");
            command.CommandText = $"""
                SELECT d.name,
                       COALESCE(COUNT(h.id), 0) AS reviewed_count,
                       COALESCE(SUM(CASE WHEN h.rating IN (3, 4) THEN 1 ELSE 0 END), 0) AS retained_count
                FROM flashcard_deck d
                LEFT JOIN flashcard_card c ON c.deck_id = d.id
                    AND c.is_deleted = 0
                    AND c.back_rel_path IS NULL
                LEFT JOIN flashcard_review_history h ON h.card_id = c.id
                WHERE 1 = 1
                  {deckNameFilter}
                GROUP BY d.id, d.name
                ORDER BY d.name COLLATE NOCASE, d.id;
                """;

            List<FlashcardDeckReviewHistorySummaryRecord> summaries = [];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                summaries.Add(ReadDeckReviewHistorySummary(reader));
            }

            return summaries;
        });
    }

    internal IReadOnlyList<FlashcardReviewRatingCountRecord> LoadReviewRatingCounts(
        IReadOnlyList<string> deckNames)
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            string deckNameFilter = AddDeckNameFilter(command, deckNames, "c.deck_id");
            command.CommandText = $"""
                SELECT h.rating,
                       COUNT(*) AS review_count
                FROM flashcard_review_history h
                JOIN flashcard_card c ON c.id = h.card_id
                WHERE c.is_deleted = 0
                  AND c.back_rel_path IS NULL
                  {deckNameFilter}
                GROUP BY h.rating
                ORDER BY h.rating;
                """;

            List<FlashcardReviewRatingCountRecord> counts = [];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                counts.Add(ReadReviewRatingCount(reader));
            }

            return counts;
        });
    }
}
