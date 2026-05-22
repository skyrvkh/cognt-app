using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal void SetCardSuspension(long cardId, bool isSuspended)
    {
        UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            long now = NowUnixTime();
            command.CommandText = """
                UPDATE flashcard_card
                SET is_suspended = $isSuspended,
                    suspended_at = $suspendedAt,
                    updated_at = $updatedAt
                WHERE id = $id
                  AND is_deleted = 0
                  AND back_rel_path IS NULL;
                """;
            command.Parameters.AddWithValue("$id", cardId);
            command.Parameters.AddWithValue("$isSuspended", isSuspended ? 1 : 0);
            command.Parameters.AddWithValue("$suspendedAt", isSuspended ? now : DBNull.Value);
            command.Parameters.AddWithValue("$updatedAt", now);

            EnsureCardUpdated(cardId, command.ExecuteNonQuery());
        });
    }

    internal void PostponeCardDueDate(
        long cardId,
        DateTimeOffset dueUtc,
        string serializedState,
        int scheduledDays)
    {
        UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE flashcard_card
                SET fsrs_card_state_json = $fsrsCardStateJson,
                    due_at = $dueAt,
                    scheduled_days = $scheduledDays,
                    updated_at = $updatedAt
                WHERE id = $id
                  AND is_deleted = 0
                  AND back_rel_path IS NULL;
                """;
            command.Parameters.AddWithValue("$id", cardId);
            command.Parameters.AddWithValue("$fsrsCardStateJson", serializedState);
            command.Parameters.AddWithValue("$dueAt", dueUtc.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("$scheduledDays", Math.Max(0, scheduledDays));
            command.Parameters.AddWithValue("$updatedAt", NowUnixTime());

            EnsureCardUpdated(cardId, command.ExecuteNonQuery());
        });
    }

    private static void EnsureCardUpdated(long cardId, int affectedRows)
    {
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Flashcard {cardId} does not exist.");
        }
    }
}
