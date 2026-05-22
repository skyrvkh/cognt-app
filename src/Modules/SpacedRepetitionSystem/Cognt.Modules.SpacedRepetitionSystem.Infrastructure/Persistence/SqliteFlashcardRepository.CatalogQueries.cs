using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal IReadOnlyList<FlashcardCardSummaryRecord> LoadCardSummaries(
        DateTimeOffset nowUtc,
        DateTimeOffset todayStartUtc,
        DateTimeOffset tomorrowStartUtc,
        long? deckId = null,
        FlashcardCardsFilterKind filter = FlashcardCardsFilterKind.All,
        string? sourceRelPath = null,
        int limit = 500)
    {
        int boundedLimit = Math.Clamp(limit, 1, 1000);
        long now = nowUtc.ToUnixTimeSeconds();
        long todayStart = todayStartUtc.ToUnixTimeSeconds();
        long tomorrowStart = tomorrowStartUtc.ToUnixTimeSeconds();

        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                WITH card_metrics AS (
                    SELECT c.id, c.deck_id, d.name, c.front_rel_path, c.source_rel_path,
                           c.due_at, c.state,
                           latest.rating, latest.reviewed_at,
                           c.is_suspended,
                           COALESCE((
                               SELECT COUNT(*)
                               FROM flashcard_review_history h
                               WHERE h.card_id = c.id
                                 AND h.reviewed_at >= $todayStart
                                 AND h.reviewed_at < $tomorrowStart
                           ), 0) AS reviewed_today_count
                    FROM flashcard_card c
                    JOIN flashcard_deck d ON d.id = c.deck_id
                    LEFT JOIN (
                        SELECT h.card_id, h.rating, h.reviewed_at
                        FROM flashcard_review_history h
                        JOIN (
                            SELECT card_id, MAX(reviewed_at) AS reviewed_at
                            FROM flashcard_review_history
                            GROUP BY card_id
                        ) latest_h ON latest_h.card_id = h.card_id AND latest_h.reviewed_at = h.reviewed_at
                    ) latest ON latest.card_id = c.id
                    WHERE c.is_deleted = 0
                      AND c.back_rel_path IS NULL
                )
                SELECT id, name, front_rel_path, source_rel_path,
                       due_at, state, rating, reviewed_at, is_suspended
                FROM card_metrics
                WHERE ($deckId IS NULL OR deck_id = $deckId)
                  AND ($sourceRelPath IS NULL OR source_rel_path = $sourceRelPath)
                  AND (
                      $filter = 0
                      OR ($filter = 1 AND due_at <= $now AND is_suspended = 0)
                      OR ($filter = 2 AND state = {NewStateValue})
                      OR ($filter = 3 AND state IN ({LearningStateValue}, {RelearningStateValue}))
                      OR ($filter = 4 AND reviewed_today_count > 0)
                      OR ($filter = 5 AND is_suspended = 1)
                  )
                ORDER BY due_at, id
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$deckId", DbValue(deckId));
            command.Parameters.AddWithValue("$sourceRelPath", DbValue(NormalizeOptionalRelPath(sourceRelPath)));
            command.Parameters.AddWithValue("$filter", (int)filter);
            command.Parameters.AddWithValue("$now", now);
            command.Parameters.AddWithValue("$todayStart", todayStart);
            command.Parameters.AddWithValue("$tomorrowStart", tomorrowStart);
            command.Parameters.AddWithValue("$limit", boundedLimit);

            List<FlashcardCardSummaryRecord> summaries = [];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                summaries.Add(ReadCardSummary(reader));
            }

            return summaries;
        });
    }
}
