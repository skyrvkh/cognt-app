using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    private static FlashcardReviewQueueCounts ReadReviewQueueCounts(SqliteDataReader reader)
    {
        return new FlashcardReviewQueueCounts(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5));
    }

    private static FlashcardReviewLimitUsage ReadReviewLimitUsage(SqliteDataReader reader)
    {
        return new FlashcardReviewLimitUsage(
            reader.GetInt32(0),
            reader.GetInt32(1));
    }

    private static IReadOnlyList<FlashcardReviewQueueCandidate> LoadCandidatesByKind(
        SqliteConnection connection,
        FlashcardReviewQueueCategory category,
        string sql,
        ReviewQueueQueryBounds bounds,
        int limit)
    {
        int boundedLimit = Math.Max(0, limit);
        if (boundedLimit == 0)
        {
            return [];
        }

        List<FlashcardReviewQueueCandidate> candidates = [];
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$now", bounds.Now);
        command.Parameters.AddWithValue("$todayStart", bounds.TodayStart);
        command.Parameters.AddWithValue("$tomorrowStart", bounds.TomorrowStart);
        command.Parameters.AddWithValue("$limit", boundedLimit);

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            candidates.Add(new FlashcardReviewQueueCandidate(
                reader.GetInt64(0),
                category,
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1))));
        }

        return candidates;
    }

    private DateTimeOffset? LoadNextFutureDueUtcFromSql(DateTimeOffset nowUtc, string sql)
    {
        long now = nowUtc.ToUnixTimeSeconds();

        return UseConnection<DateTimeOffset?>(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$now", now);

            return ToOptionalUnixTime(command.ExecuteScalar());
        });
    }

    private static DateTimeOffset? ToOptionalUnixTime(object? scalar)
    {
        if (scalar is null || scalar is DBNull)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(scalar));
    }

    private readonly struct ReviewQueueQueryBounds
    {
        internal ReviewQueueQueryBounds(long Now, long TodayStart, long TomorrowStart)
        {
            this.Now = Now;
            this.TodayStart = TodayStart;
            this.TomorrowStart = TomorrowStart;
        }

        internal long Now { get; }

        internal long TodayStart { get; }

        internal long TomorrowStart { get; }
    }
}
