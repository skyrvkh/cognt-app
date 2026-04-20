using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal void MarkCardsDeleted(IReadOnlyCollection<long> cardIds)
    {
        if (cardIds.Count == 0)
        {
            return;
        }

        UseConnection(connection =>
        {
            long now = NowUnixTime();
            foreach (long cardId in cardIds.Where(id => id > 0).Distinct())
            {
                MarkCardDeleted(connection, cardId, now);
            }
        });
    }

    private static void MarkCardDeleted(
        SqliteConnection connection,
        long cardId,
        long updatedAt)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE flashcard_card
            SET is_deleted = 1, updated_at = $updatedAt
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", cardId);
        command.Parameters.AddWithValue("$updatedAt", updatedAt);
        command.ExecuteNonQuery();
    }
}
