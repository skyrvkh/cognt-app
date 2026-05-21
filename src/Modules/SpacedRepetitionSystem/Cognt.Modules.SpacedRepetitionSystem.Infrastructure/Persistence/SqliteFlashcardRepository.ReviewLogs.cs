using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal IReadOnlyList<FsrsReviewLog> LoadFsrsReviewLogs()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT review_log_json
                FROM flashcard_review_history
                WHERE review_log_json IS NOT NULL
                  AND review_log_json <> ''
                ORDER BY card_id, reviewed_at, id;
                """;

            List<FsrsReviewLog> reviewLogs = [];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (TryReadReviewLog(reader.GetString(0), out FsrsReviewLog? reviewLog) && reviewLog is not null)
                {
                    reviewLogs.Add(reviewLog);
                }
            }

            return reviewLogs;
        });
    }

    private static bool TryReadReviewLog(string reviewLogJson, out FsrsReviewLog? reviewLog)
    {
        try
        {
            reviewLog = FsrsReviewLog.FromJson(reviewLogJson);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or System.Text.Json.JsonException)
        {
            reviewLog = null;
            return false;
        }
    }
}
