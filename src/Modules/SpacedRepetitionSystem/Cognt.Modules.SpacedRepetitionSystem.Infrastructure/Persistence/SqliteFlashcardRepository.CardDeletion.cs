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

        long[] normalizedCardIds = cardIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();
        if (normalizedCardIds.Length == 0)
        {
            return;
        }

        UseConnection(connection =>
        {
            long now = NowUnixTime();
            foreach (long cardId in normalizedCardIds)
            {
                MarkCardDeleted(connection, cardId, now);
            }
        });
    }

    private static int MarkCardDeleted(
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
        return command.ExecuteNonQuery();
    }
}
