using Cognt.Modules.SpacedRepetitionSystem.Application.Analysis;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal FlashcardDashboardSummaryRecord LoadDashboardSummary(
        DateTimeOffset nowUtc,
        DateTimeOffset todayStartUtc,
        DateTimeOffset tomorrowStartUtc,
        IReadOnlyList<string> deckNames)
    {
        long now = nowUtc.ToUnixTimeSeconds();
        long todayStart = todayStartUtc.ToUnixTimeSeconds();
        long tomorrowStart = tomorrowStartUtc.ToUnixTimeSeconds();
        long overdueCutoff = nowUtc.AddDays(-FlashcardDifficultyThresholds.OverdueDaysMinimum).ToUnixTimeSeconds();

        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            string deckNameFilter = AddDeckNameFilter(command, deckNames, "c.deck_id");
            command.CommandText = $"""
                WITH card_metrics AS (
                    SELECT c.id, c.due_at, c.state, c.difficulty,
                           COALESCE((
                               SELECT COUNT(*)
                               FROM flashcard_review_history h
                               WHERE h.card_id = c.id
                                 AND h.rating = 1
                           ), 0) AS lapse_count,
                           COALESCE((
                               SELECT COUNT(*)
                               FROM (
                                   SELECT h.rating
                                   FROM flashcard_review_history h
                                   WHERE h.card_id = c.id
                                   ORDER BY h.reviewed_at DESC, h.id DESC
                                   LIMIT $recentRatingWindow
                               ) recent
                               WHERE recent.rating IN (1, 2)
                           ), 0) AS recent_low_rating_count,
                           COALESCE((
                               SELECT COUNT(*)
                               FROM flashcard_review_history h
                               WHERE h.card_id = c.id
                           ), 0) AS review_count,
                           COALESCE((
                               SELECT COUNT(*)
                               FROM flashcard_review_history h
                               WHERE h.card_id = c.id
                                 AND h.reviewed_at >= $todayStart
                                 AND h.reviewed_at < $tomorrowStart
                           ), 0) AS reviewed_today_count
                    FROM flashcard_card c
                    WHERE c.is_deleted = 0
                      AND c.back_rel_path IS NULL
                      {deckNameFilter}
                )
                SELECT COUNT(*) AS total_cards,
                       COALESCE(SUM(CASE WHEN due_at <= $now THEN 1 ELSE 0 END), 0) AS due_cards,
                       COALESCE(SUM(CASE WHEN state = {NewStateValue} THEN 1 ELSE 0 END), 0) AS new_cards,
                       COALESCE(SUM(CASE WHEN reviewed_today_count > 0 THEN 1 ELSE 0 END), 0) AS reviewed_today_cards,
                       COALESCE(SUM(CASE
                           WHEN difficulty >= $difficultyThreshold
                             OR lapse_count >= $lapseThreshold
                             OR recent_low_rating_count >= $recentLowThreshold
                             OR due_at <= $overdueCutoff
                           THEN 1 ELSE 0 END), 0) AS difficult_cards,
                       COALESCE(SUM(review_count), 0) AS review_history_count
                FROM card_metrics;
                """;
            command.Parameters.AddWithValue("$now", now);
            command.Parameters.AddWithValue("$todayStart", todayStart);
            command.Parameters.AddWithValue("$tomorrowStart", tomorrowStart);
            command.Parameters.AddWithValue("$difficultyThreshold", FlashcardDifficultyThresholds.HighDifficultyMinimum);
            command.Parameters.AddWithValue("$lapseThreshold", FlashcardDifficultyThresholds.LapseCountMinimum);
            command.Parameters.AddWithValue("$recentLowThreshold", FlashcardDifficultyThresholds.RecentLowRatingMinimum);
            command.Parameters.AddWithValue("$recentRatingWindow", FlashcardDifficultyThresholds.RecentRatingWindow);
            command.Parameters.AddWithValue("$overdueCutoff", overdueCutoff);

            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read()
                ? ReadDashboardSummary(reader)
                : new FlashcardDashboardSummaryRecord(0, 0, 0, 0, 0, 0);
        });
    }
}
