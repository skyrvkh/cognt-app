using Microsoft.Data.Sqlite;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Tags;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    private static IReadOnlyList<long> LoadExistingItemIds(
        SqliteConnection connection,
        IReadOnlyList<long> itemIds,
        SqliteTransaction? transaction = null)
    {
        List<long> existing = [];
        for (int offset = 0; offset < itemIds.Count; offset += 400)
        {
            int count = Math.Min(400, itemIds.Count - offset);
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            string[] parameterNames = CreateParameterNames(count, "$id");
            command.CommandText = $"""
                SELECT id
                FROM item
                WHERE is_deleted = 0
                  AND id IN ({string.Join(", ", parameterNames)})
                ORDER BY id;
                """;
            AddParameters(command, parameterNames, itemIds, offset, count);
            existing.AddRange(ReadRows(command, static reader => reader.GetInt64(0)));
        }

        return SortDistinctIds(existing);
    }

    private static long UpsertTag(
        SqliteConnection connection,
        SqliteTransaction transaction,
        KnowledgeTagValue tag,
        long now)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO tag (normalized_name, display_name, created_at, updated_at)
            VALUES ($normalizedName, $displayName, $createdAt, $updatedAt)
            ON CONFLICT(normalized_name) DO UPDATE
            SET display_name = CASE
                    WHEN length(excluded.display_name) > 0 THEN excluded.display_name
                    ELSE tag.display_name
                END,
                updated_at = excluded.updated_at
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$normalizedName", tag.NormalizedName);
        command.Parameters.AddWithValue("$displayName", tag.DisplayName);
        command.Parameters.AddWithValue("$createdAt", now);
        command.Parameters.AddWithValue("$updatedAt", now);
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static void InsertItemTag(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long itemId,
        long tagId,
        long now)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO item_tag (item_id, tag_id, created_at)
            VALUES ($itemId, $tagId, $createdAt);
            """;
        command.Parameters.AddWithValue("$itemId", itemId);
        command.Parameters.AddWithValue("$tagId", tagId);
        command.Parameters.AddWithValue("$createdAt", now);
        command.ExecuteNonQuery();
    }

    private static void TouchItems(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<long> itemIds,
        long updatedAt)
    {
        for (int offset = 0; offset < itemIds.Count; offset += 400)
        {
            int count = Math.Min(400, itemIds.Count - offset);
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            string[] parameterNames = CreateParameterNames(count, "$id");
            command.CommandText = $"""
                UPDATE item
                SET updated_at = $updatedAt
                WHERE id IN ({string.Join(", ", parameterNames)});
            """;
            command.Parameters.AddWithValue("$updatedAt", updatedAt);
            AddParameters(command, parameterNames, itemIds, offset, count);
            command.ExecuteNonQuery();
        }
    }

    private static string[] CreateParameterNames(int count, string prefix)
    {
        return Enumerable.Range(0, count)
            .Select(index => string.Concat(prefix, index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
    }

    private static List<T> ReadRows<T>(SqliteCommand command, Func<SqliteDataReader, T> read)
    {
        List<T> rows = [];
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    private static void AddParameters<T>(
        SqliteCommand command,
        IReadOnlyList<string> parameterNames,
        IReadOnlyList<T> values,
        int offset,
        int count)
    {
        for (int i = 0; i < count; i++)
        {
            command.Parameters.AddWithValue(parameterNames[i], values[offset + i]!);
        }
    }

    private static long[] NormalizeItemIds(IReadOnlyList<long> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        return itemIds.Where(static id => id > 0).Distinct().Order().ToArray();
    }

    private static long[] SortDistinctIds(List<long> itemIds)
    {
        return itemIds.Distinct().Order().ToArray();
    }
}
