using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal FlashcardReviewQueueCounts LoadReviewQueueCounts(
        DateTimeOffset nowUtc,
        DateTimeOffset todayStartUtc,
        DateTimeOffset tomorrowStartUtc)
    {
        long now = nowUtc.ToUnixTimeSeconds();
        long todayStart = todayStartUtc.ToUnixTimeSeconds();
        long tomorrowStart = tomorrowStartUtc.ToUnixTimeSeconds();

        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT
                    COALESCE(SUM(CASE WHEN is_suspended = 0 AND state = {LearningStateValue} AND due_at <= $now THEN 1 ELSE 0 END), 0) AS learning_due_cards,
                    COALESCE(SUM(CASE WHEN is_suspended = 0 AND state = {RelearningStateValue} AND due_at <= $now THEN 1 ELSE 0 END), 0) AS relearning_due_cards,
                    COALESCE(SUM(CASE WHEN is_suspended = 0 AND state = {ReviewStateValue} AND due_at < $todayStart THEN 1 ELSE 0 END), 0) AS overdue_cards,
                    COALESCE(SUM(CASE
                        WHEN is_suspended = 0
                         AND state = {ReviewStateValue}
                         AND due_at >= $todayStart
                         AND due_at < $tomorrowStart
                         AND due_at <= $now
                        THEN 1 ELSE 0 END), 0) AS due_cards,
                    COALESCE(SUM(CASE WHEN is_suspended = 0 AND state = {NewStateValue} THEN 1 ELSE 0 END), 0) AS new_cards,
                    COALESCE(SUM(CASE
                        WHEN is_suspended = 1
                         AND (
                             state = {NewStateValue}
                             OR (state IN ({LearningStateValue}, {RelearningStateValue}) AND due_at <= $now)
                             OR (state = {ReviewStateValue} AND due_at < $tomorrowStart AND due_at <= $now)
                         )
                        THEN 1 ELSE 0 END), 0) AS suspended_cards_excluded
                FROM flashcard_card
                WHERE is_deleted = 0
                  AND back_rel_path IS NULL;
                """;
            command.Parameters.AddWithValue("$now", now);
            command.Parameters.AddWithValue("$todayStart", todayStart);
            command.Parameters.AddWithValue("$tomorrowStart", tomorrowStart);

            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read()
                ? ReadReviewQueueCounts(reader)
                : new FlashcardReviewQueueCounts(0, 0, 0, 0, 0, 0);
        });
    }

    internal FlashcardReviewLimitUsage LoadReviewLimitUsage(
        DateTimeOffset todayStartUtc,
        DateTimeOffset tomorrowStartUtc)
    {
        long todayStart = todayStartUtc.ToUnixTimeSeconds();
        long tomorrowStart = tomorrowStartUtc.ToUnixTimeSeconds();

        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT
                    COALESCE(SUM(CASE WHEN h.previous_state = {ReviewStateValue} THEN 1 ELSE 0 END), 0) AS reviewed_cards,
                    COALESCE(SUM(CASE WHEN h.previous_state = {NewStateValue} THEN 1 ELSE 0 END), 0) AS new_cards
                FROM flashcard_review_history h
                JOIN flashcard_card c ON c.id = h.card_id
                WHERE c.is_deleted = 0
                  AND c.back_rel_path IS NULL
                  AND h.reviewed_at >= $todayStart
                  AND h.reviewed_at < $tomorrowStart;
                """;
            command.Parameters.AddWithValue("$todayStart", todayStart);
            command.Parameters.AddWithValue("$tomorrowStart", tomorrowStart);

            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read()
                ? ReadReviewLimitUsage(reader)
                : new FlashcardReviewLimitUsage(0, 0);
        });
    }

    internal DateTimeOffset? LoadNextFutureDueUtc(DateTimeOffset nowUtc)
    {
        return LoadNextFutureDueUtcFromSql(
            nowUtc,
            $"""
            SELECT MIN(due_at)
            FROM flashcard_card
            WHERE {ActiveSingleFileCardPredicate}
              AND state IN ({LearningStateValue}, {ReviewStateValue}, {RelearningStateValue})
              AND due_at > $now;
            """);
    }

    internal DateTimeOffset? LoadNextFutureIntradayDueUtc(DateTimeOffset nowUtc)
    {
        return LoadNextFutureDueUtcFromSql(
            nowUtc,
            $"""
            SELECT MIN(due_at)
            FROM flashcard_card
            WHERE {ActiveSingleFileCardPredicate}
              AND state IN ({LearningStateValue}, {RelearningStateValue})
              AND due_at > $now;
            """);
    }
}
