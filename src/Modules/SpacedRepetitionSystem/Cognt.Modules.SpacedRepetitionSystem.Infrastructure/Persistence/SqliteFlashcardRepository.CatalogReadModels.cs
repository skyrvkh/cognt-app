using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal IReadOnlyList<FlashcardDeckSummaryRecord> LoadDeckSummaries(DateTimeOffset nowUtc)
    {
        long now = nowUtc.ToUnixTimeSeconds();

        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT d.id,
                       d.name,
                       COALESCE(SUM(CASE WHEN c.id IS NOT NULL AND c.state = {NewStateValue} THEN 1 ELSE 0 END), 0) AS new_cards,
                       COALESCE(SUM(CASE WHEN c.id IS NOT NULL AND c.state IN ({LearningStateValue}, {RelearningStateValue}) THEN 1 ELSE 0 END), 0) AS learning_cards,
                       COALESCE(SUM(CASE WHEN c.id IS NOT NULL AND c.due_at <= $now THEN 1 ELSE 0 END), 0) AS due_cards,
                       COALESCE(COUNT(c.id), 0) AS total_cards
                FROM flashcard_deck d
                LEFT JOIN flashcard_card c ON c.deck_id = d.id
                    AND c.is_deleted = 0
                    AND c.back_rel_path IS NULL
                GROUP BY d.id, d.name
                ORDER BY d.name COLLATE NOCASE, d.id;
                """;
            command.Parameters.AddWithValue("$now", now);

            List<FlashcardDeckSummaryRecord> summaries = [];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                summaries.Add(ReadDeckSummary(reader));
            }

            return summaries;
        });
    }

    internal IReadOnlyList<FlashcardFileReferenceRecord> LoadActiveCardFileReferences()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, front_rel_path
                FROM flashcard_card
                WHERE is_deleted = 0
                  AND back_rel_path IS NULL
                ORDER BY id;
                """;

            List<FlashcardFileReferenceRecord> references = [];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                references.Add(ReadFileReference(reader));
            }

            return references;
        });
    }

    private static FlashcardDeckSummaryRecord ReadDeckSummary(SqliteDataReader reader)
    {
        return new FlashcardDeckSummaryRecord(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5));
    }

    private static FlashcardFileReferenceRecord ReadFileReference(SqliteDataReader reader)
    {
        return new FlashcardFileReferenceRecord(
            reader.GetInt64(0),
            reader.GetString(1));
    }

    private static FlashcardCardSummaryRecord ReadCardSummary(SqliteDataReader reader)
    {
        return new FlashcardCardSummaryRecord(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(4)),
            (FlashcardLearningState)reader.GetInt32(5),
            reader.IsDBNull(6) ? null : (FlashcardReviewRating)reader.GetInt32(6),
            reader.IsDBNull(7) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(7)));
    }
}
