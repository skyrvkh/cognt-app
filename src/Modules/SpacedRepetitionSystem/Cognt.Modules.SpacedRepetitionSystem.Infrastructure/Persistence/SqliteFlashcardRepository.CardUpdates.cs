using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal void UpdateCardDeck(long cardId, long deckId)
    {
        UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE flashcard_card
                SET deck_id = $deckId,
                    updated_at = $updatedAt
                WHERE id = $id
                  AND is_deleted = 0
                  AND back_rel_path IS NULL;
                """;
            command.Parameters.AddWithValue("$id", cardId);
            command.Parameters.AddWithValue("$deckId", deckId);
            command.Parameters.AddWithValue("$updatedAt", NowUnixTime());
            command.ExecuteNonQuery();
        });
    }
}
