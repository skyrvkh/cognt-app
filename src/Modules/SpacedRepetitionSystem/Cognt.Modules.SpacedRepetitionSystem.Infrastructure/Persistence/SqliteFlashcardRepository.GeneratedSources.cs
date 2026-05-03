using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal IReadOnlyList<GeneratedFlashcardSourceRecord> LoadActiveGeneratedCardsForContext(
        string sourceRelPath,
        string sourceContextId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContextId);

        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, source_rel_path, deck_id, front_rel_path, source_context_id, source_slot_id, source_snapshot_hash
                FROM flashcard_card
                WHERE is_deleted = 0
                  AND back_rel_path IS NULL
                  AND source_rel_path = $sourceRelPath
                  AND source_context_id = $sourceContextId
                ORDER BY id;
                """;
            command.Parameters.AddWithValue("$sourceRelPath", NormalizeRelPath(sourceRelPath));
            command.Parameters.AddWithValue("$sourceContextId", sourceContextId.Trim());

            List<GeneratedFlashcardSourceRecord> records = [];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                records.Add(new GeneratedFlashcardSourceRecord(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetInt64(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6)));
            }

            return records;
        });
    }

    internal IReadOnlyList<GeneratedFlashcardSourceRecord> LoadActiveGeneratedCardsForSourceNote(string sourceRelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelPath);

        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, source_rel_path, deck_id, front_rel_path, source_context_id, source_slot_id, source_snapshot_hash
                FROM flashcard_card
                WHERE is_deleted = 0
                  AND back_rel_path IS NULL
                  AND source_rel_path = $sourceRelPath
                  AND source_context_id IS NOT NULL
                  AND source_slot_id IS NOT NULL
                ORDER BY source_context_id, source_slot_id, id;
                """;
            command.Parameters.AddWithValue("$sourceRelPath", NormalizeRelPath(sourceRelPath));

            List<GeneratedFlashcardSourceRecord> records = [];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                records.Add(new GeneratedFlashcardSourceRecord(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetInt64(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6)));
            }

            return records;
        });
    }

}
