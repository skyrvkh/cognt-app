using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal IReadOnlyList<FlashcardScheduledDueRecord> LoadScheduledDueCards(
        DateTimeOffset endUtc,
        IReadOnlyList<string> deckNames)
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            string deckNameFilter = AddDeckNameFilter(command, deckNames, "c.deck_id");
            command.CommandText = $"""
                SELECT c.due_at,
                       d.name
                FROM flashcard_card c
                JOIN flashcard_deck d ON d.id = c.deck_id
                WHERE c.is_deleted = 0
                  AND c.is_suspended = 0
                  AND c.back_rel_path IS NULL
                  AND c.due_at < $end
                  {deckNameFilter}
                ORDER BY c.due_at, c.id;
                """;
            command.Parameters.AddWithValue("$end", endUtc.ToUnixTimeSeconds());

            List<FlashcardScheduledDueRecord> records = [];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                records.Add(new FlashcardScheduledDueRecord(
                    DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)),
                    reader.GetString(1)));
            }

            return records;
        });
    }

    internal IReadOnlyList<FlashcardTomorrowWorkloadContributorRecord> LoadTomorrowWorkloadContributors(
        DateTimeOffset todayStartUtc,
        DateTimeOffset tomorrowStartUtc,
        DateTimeOffset dayAfterTomorrowStartUtc,
        IReadOnlyList<string> deckNames)
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            string deckNameFilter = AddDeckNameFilter(command, deckNames, "c.deck_id");
            command.CommandText = $"""
                SELECT d.name,
                       COUNT(DISTINCT h.card_id) AS scheduled_count
                FROM flashcard_review_history h
                JOIN flashcard_card c ON c.id = h.card_id
                JOIN flashcard_deck d ON d.id = c.deck_id
                WHERE c.is_deleted = 0
                  AND c.back_rel_path IS NULL
                  AND h.reviewed_at >= $todayStart
                  AND h.reviewed_at < $tomorrowStart
                  AND h.next_due_at >= $tomorrowStart
                  AND h.next_due_at < $dayAfterTomorrowStart
                  {deckNameFilter}
                GROUP BY d.id, d.name
                ORDER BY scheduled_count DESC, d.name COLLATE NOCASE, d.id;
                """;
            command.Parameters.AddWithValue("$todayStart", todayStartUtc.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("$tomorrowStart", tomorrowStartUtc.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("$dayAfterTomorrowStart", dayAfterTomorrowStartUtc.ToUnixTimeSeconds());

            List<FlashcardTomorrowWorkloadContributorRecord> records = [];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                records.Add(new FlashcardTomorrowWorkloadContributorRecord(
                    reader.GetString(0),
                    reader.GetInt32(1)));
            }

            return records;
        });
    }

    internal FlashcardLearningDiagnosticsRecord LoadLearningDiagnostics(
        IReadOnlyList<string> deckNames)
    {
        return UseConnection(connection =>
        {
            List<FlashcardLearningDiagnosticCardRecord> cards = LoadLearningDiagnosticCards(connection, deckNames);
            List<int> durations = LoadReviewDurations(connection, deckNames);
            return new FlashcardLearningDiagnosticsRecord(cards, durations);
        });
    }

    private static List<FlashcardLearningDiagnosticCardRecord> LoadLearningDiagnosticCards(
        SqliteConnection connection,
        IReadOnlyList<string> deckNames)
    {
        using SqliteCommand command = connection.CreateCommand();
        string deckNameFilter = AddDeckNameFilter(command, deckNames, "c.deck_id");
        command.CommandText = $"""
            SELECT c.stability,
                   c.difficulty,
                   c.lapses
            FROM flashcard_card c
            WHERE c.is_deleted = 0
              AND c.back_rel_path IS NULL
              {deckNameFilter};
            """;

        List<FlashcardLearningDiagnosticCardRecord> cards = [];
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            cards.Add(new FlashcardLearningDiagnosticCardRecord(
                reader.IsDBNull(0) ? null : reader.GetDouble(0),
                reader.IsDBNull(1) ? null : reader.GetDouble(1),
                reader.GetInt32(2)));
        }

        return cards;
    }

    private List<int> LoadReviewDurations(
        SqliteConnection connection,
        IReadOnlyList<string> deckNames)
    {
        using SqliteCommand command = connection.CreateCommand();
        string deckNameFilter = AddDeckNameFilter(command, deckNames, "c.deck_id");
        command.CommandText = $"""
            SELECT h.review_log_json
            FROM flashcard_review_history h
            JOIN flashcard_card c ON c.id = h.card_id
            WHERE c.is_deleted = 0
              AND c.back_rel_path IS NULL
              AND h.review_log_json IS NOT NULL
              {deckNameFilter};
            """;

        List<int> durations = [];
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string reviewLogJson = reader.GetString(0);
            try
            {
                FsrsReviewLog reviewLog = FsrsReviewLog.FromJson(reviewLogJson);
                if (reviewLog.ReviewDuration is > 0)
                {
                    durations.Add(reviewLog.ReviewDuration.Value);
                }
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException or ArgumentOutOfRangeException)
            {
                logger?.LogDebug(ex, "Skipped malformed review duration log. DatabasePath={DatabasePath}", databasePath);
            }
        }

        return durations;
    }
}
