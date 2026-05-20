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

    private static FlashcardReviewQueueCounts ReadCramReviewQueueCounts(SqliteDataReader reader)
    {
        return new FlashcardReviewQueueCounts(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5))
        {
            FutureReviewCards = reader.GetInt32(6),
        };
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

    private static IReadOnlyList<FlashcardReviewQueueCandidate> LoadCandidatesByDynamicKind(
        SqliteConnection connection,
        string sql,
        ReviewQueueQueryBounds bounds,
        Action<SqliteCommand>? configureCommand = null)
    {
        List<FlashcardReviewQueueCandidate> candidates = [];
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$now", bounds.Now);
        command.Parameters.AddWithValue("$todayStart", bounds.TodayStart);
        command.Parameters.AddWithValue("$tomorrowStart", bounds.TomorrowStart);
        configureCommand?.Invoke(command);

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            candidates.Add(new FlashcardReviewQueueCandidate(
                reader.GetInt64(0),
                (FlashcardReviewQueueCategory)reader.GetInt32(2),
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1))));
        }

        return candidates;
    }

    private static long[] NormalizeDeckScope(IReadOnlySet<long> deckIds)
    {
        return deckIds
            .Where(id => id > 0)
            .Order()
            .ToArray();
    }

    private static string CreateDeckFilterClause(IReadOnlyList<long> deckIds)
    {
        return deckIds.Count == 0
            ? string.Empty
            : $"AND deck_id IN ({string.Join(", ", CreateDeckParameterNames(deckIds.Count))})";
    }

    private static void AddDeckFilterParameters(SqliteCommand command, IReadOnlyList<long> deckIds)
    {
        string[] names = CreateDeckParameterNames(deckIds.Count);
        for (int index = 0; index < deckIds.Count; index++)
        {
            command.Parameters.AddWithValue(names[index], deckIds[index]);
        }
    }

    private static string[] CreateDeckParameterNames(int count)
    {
        return Enumerable
            .Range(0, count)
            .Select(index => string.Concat("$deckId", index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
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
