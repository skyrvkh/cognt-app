using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal FlashcardDueCardSummaryRecord LoadDueCardSummary(
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
                    COUNT(*) AS due_cards,
                    COUNT(DISTINCT c.deck_id) AS due_decks
                FROM flashcard_card c
                WHERE c.is_deleted = 0
                  AND c.back_rel_path IS NULL
                  AND c.due_at >= $start
                  AND c.due_at < $end
                  {deckNameFilter};
                """;
            command.Parameters.AddWithValue("$start", start);
            command.Parameters.AddWithValue("$end", end);

            using SqliteDataReader reader = command.ExecuteReader();
            reader.Read();
            return new FlashcardDueCardSummaryRecord(
                reader.GetInt32(0),
                reader.GetInt32(1));
        });
    }
}
