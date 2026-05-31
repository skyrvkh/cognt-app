using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal void ApplyReview(long cardId, FlashcardReviewRatingReadModel rating, FlashcardSchedule schedule)
    {
        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            FlashcardRecord previous = GetCard(cardId, connection, transaction);

            long now = NowUnixTime();

            UpdateReviewedCardSchedule(connection, transaction, cardId, schedule, now);
            InsertReviewHistory(connection, transaction, cardId, previous, rating, schedule, now);

            transaction.Commit();
        });
    }

    private static void UpdateReviewedCardSchedule(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long cardId,
        FlashcardSchedule schedule,
        long updatedAt)
    {
        using SqliteCommand updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = """
            UPDATE flashcard_card
            SET fsrs_card_state_json = $fsrsCardStateJson,
                due_at = $dueAt,
                state = $state,
                stability = $stability,
                difficulty = $difficulty,
                fsrs_step = $fsrsStep,
                last_review_at = $lastReviewAt,
                elapsed_days = $elapsedDays,
                scheduled_days = $scheduledDays,
                reps = $reps,
                lapses = $lapses,
                updated_at = $updatedAt
            WHERE id = $id
              AND is_deleted = 0;
            """;
        updateCommand.Parameters.AddWithValue("$id", cardId);
        AddScheduleParameters(updateCommand, schedule);
        updateCommand.Parameters.AddWithValue("$updatedAt", updatedAt);
        updateCommand.ExecuteNonQuery();
    }

    private static void InsertReviewHistory(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long cardId,
        FlashcardRecord previous,
        FlashcardReviewRatingReadModel rating,
        FlashcardSchedule schedule,
        long reviewedAt)
    {
        using SqliteCommand insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            INSERT INTO flashcard_review_history (
                uid, card_id, reviewed_at, rating, previous_due_at, next_due_at,
                previous_state, next_state, review_log_json
            )
            VALUES (
                $uid, $cardId, $reviewedAt, $rating, $previousDueAt, $nextDueAt,
                $previousState, $nextState, $reviewLogJson
            );
            """;
        insertCommand.Parameters.AddWithValue("$uid", Guid.NewGuid().ToString("N"));
        insertCommand.Parameters.AddWithValue("$cardId", cardId);
        insertCommand.Parameters.AddWithValue("$reviewedAt", reviewedAt);
        insertCommand.Parameters.AddWithValue("$rating", (int)rating);
        insertCommand.Parameters.AddWithValue("$previousDueAt", previous.DueUtc.ToUnixTimeSeconds());
        insertCommand.Parameters.AddWithValue("$nextDueAt", schedule.DueUtc.ToUnixTimeSeconds());
        insertCommand.Parameters.AddWithValue("$previousState", (int)previous.State);
        insertCommand.Parameters.AddWithValue("$nextState", (int)schedule.State);
        insertCommand.Parameters.AddWithValue("$reviewLogJson", DbValue(schedule.ReviewLog));
        insertCommand.ExecuteNonQuery();
    }
}
