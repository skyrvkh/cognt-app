using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    IReadOnlyList<long> ITreeRepository.ExpandItemIds(
        IReadOnlyList<long> rootItemIds,
        bool includeDescendants)
    {
        long[] ids = NormalizeItemIds(rootItemIds);
        if (ids.Length == 0)
        {
            return [];
        }

        return UseConnection(connection =>
        {
            if (!includeDescendants)
            {
                return LoadExistingItemIds(connection, ids);
            }

            List<long> expanded = [];
            foreach (long[] chunk in ids.Chunk(400))
            {
                using SqliteCommand command = connection.CreateCommand();
                string[] parameterNames = CreateParameterNames(chunk.Length, "$id");
                command.CommandText = $"""
                    WITH RECURSIVE selected_items(id) AS (
                      SELECT id
                      FROM item
                      WHERE is_deleted = 0
                        AND id IN ({string.Join(", ", parameterNames)})

                      UNION

                      SELECT child.id
                      FROM item child
                      JOIN selected_items selected ON child.parent_id = selected.id
                      WHERE child.is_deleted = 0
                    )
                    SELECT DISTINCT id
                    FROM selected_items
                    ORDER BY id;
                    """;
                AddIdParameters(command, parameterNames, chunk);

                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    expanded.Add(reader.GetInt64(0));
                }
            }

            return expanded.Distinct().OrderBy(id => id).ToArray();
        });
    }

    IReadOnlyList<ItemTagRow> ITreeRepository.LoadTagsByItemIds(IReadOnlyList<long> itemIds)
    {
        long[] ids = NormalizeItemIds(itemIds);
        if (ids.Length == 0)
        {
            return [];
        }

        return UseConnection(connection =>
        {
            List<ItemTagRow> rows = [];
            foreach (long[] chunk in ids.Chunk(400))
            {
                using SqliteCommand command = connection.CreateCommand();
                string[] parameterNames = CreateParameterNames(chunk.Length, "$id");
                command.CommandText = $"""
                    SELECT it.item_id, t.display_name, t.normalized_name
                    FROM item_tag it
                    JOIN tag t ON t.id = it.tag_id
                    JOIN item i ON i.id = it.item_id
                    WHERE i.is_deleted = 0
                      AND it.item_id IN ({string.Join(", ", parameterNames)})
                    ORDER BY t.display_name COLLATE NOCASE, t.id;
                    """;
                AddIdParameters(command, parameterNames, chunk);

                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    rows.Add(new ItemTagRow(
                        reader.GetInt64(0),
                        reader.GetString(1),
                        reader.GetString(2)));
                }
            }

            return rows;
        });
    }

    IReadOnlyList<string> ITreeRepository.LoadTagNames()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT display_name
                FROM tag
                ORDER BY display_name COLLATE NOCASE, id;
                """;

            List<string> names = [];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                names.Add(reader.GetString(0));
            }

            return names;
        });
    }

    void ITreeRepository.AssignTagsToItems(
        IReadOnlyList<long> itemIds,
        IReadOnlyList<string> tags)
    {
        long[] ids = NormalizeItemIds(itemIds);
        TagValue[] normalizedTags = NormalizeTags(tags);
        if (ids.Length == 0 || normalizedTags.Length == 0)
        {
            return;
        }

        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            long now = GetCurrentUnixTimeSeconds();
            long[] existingItemIds = LoadExistingItemIds(connection, ids, transaction).ToArray();

            foreach (TagValue tag in normalizedTags)
            {
                long tagId = UpsertTag(connection, transaction, tag, now);
                foreach (long itemId in existingItemIds)
                {
                    InsertItemTag(connection, transaction, itemId, tagId, now);
                }
            }

            TouchItems(connection, transaction, existingItemIds, now);
            transaction.Commit();
        });
    }

    void ITreeRepository.RemoveTagsFromItems(
        IReadOnlyList<long> itemIds,
        IReadOnlyList<string> normalizedTags)
    {
        long[] ids = NormalizeItemIds(itemIds);
        string[] tags = normalizedTags
            .Select(NormalizeTagIdentity)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0 || tags.Length == 0)
        {
            return;
        }

        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            long now = GetCurrentUnixTimeSeconds();
            long[] existingItemIds = LoadExistingItemIds(connection, ids, transaction).ToArray();

            foreach (long[] itemChunk in existingItemIds.Chunk(400))
            {
                foreach (string[] tagChunk in tags.Chunk(400))
                {
                    using SqliteCommand command = connection.CreateCommand();
                    command.Transaction = transaction;
                    string[] itemParameterNames = CreateParameterNames(itemChunk.Length, "$item");
                    string[] tagParameterNames = CreateParameterNames(tagChunk.Length, "$tag");
                    command.CommandText = $"""
                        DELETE FROM item_tag
                        WHERE item_id IN ({string.Join(", ", itemParameterNames)})
                          AND tag_id IN (
                            SELECT id
                            FROM tag
                            WHERE normalized_name IN ({string.Join(", ", tagParameterNames)})
                          );
                        """;
                    AddIdParameters(command, itemParameterNames, itemChunk);
                    for (int i = 0; i < tagChunk.Length; i++)
                    {
                        command.Parameters.AddWithValue(tagParameterNames[i], tagChunk[i]);
                    }

                    command.ExecuteNonQuery();
                }
            }

            TouchItems(connection, transaction, existingItemIds, now);
            transaction.Commit();
        });
    }

    private static IReadOnlyList<long> LoadExistingItemIds(
        SqliteConnection connection,
        IReadOnlyList<long> itemIds,
        SqliteTransaction? transaction = null)
    {
        List<long> existing = [];
        foreach (long[] chunk in itemIds.Chunk(400))
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            string[] parameterNames = CreateParameterNames(chunk.Length, "$id");
            command.CommandText = $"""
                SELECT id
                FROM item
                WHERE is_deleted = 0
                  AND id IN ({string.Join(", ", parameterNames)})
                ORDER BY id;
                """;
            AddIdParameters(command, parameterNames, chunk);

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                existing.Add(reader.GetInt64(0));
            }
        }

        return existing.Distinct().OrderBy(id => id).ToArray();
    }

    private static long UpsertTag(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TagValue tag,
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
        foreach (long[] chunk in itemIds.Chunk(400))
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            string[] parameterNames = CreateParameterNames(chunk.Length, "$id");
            command.CommandText = $"""
                UPDATE item
                SET updated_at = $updatedAt
                WHERE id IN ({string.Join(", ", parameterNames)});
                """;
            command.Parameters.AddWithValue("$updatedAt", updatedAt);
            AddIdParameters(command, parameterNames, chunk);
            command.ExecuteNonQuery();
        }
    }

    private static string[] CreateParameterNames(int count, string prefix)
    {
        return Enumerable.Range(0, count)
            .Select(index => string.Concat(prefix, index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
    }

    private static void AddIdParameters(SqliteCommand command, IReadOnlyList<string> parameterNames, IReadOnlyList<long> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            command.Parameters.AddWithValue(parameterNames[i], values[i]);
        }
    }

    private static long[] NormalizeItemIds(IReadOnlyList<long> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);

        return itemIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
    }

    private static TagValue[] NormalizeTags(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return tags
            .Select(tag => new TagValue(NormalizeTagDisplayName(tag), NormalizeTagIdentity(tag)))
            .Where(tag => tag.DisplayName.Length > 0 && tag.NormalizedName.Length > 0)
            .GroupBy(tag => tag.NormalizedName, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(tag => tag.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeTagDisplayName(string? tag)
    {
        return string.IsNullOrWhiteSpace(tag)
            ? string.Empty
            : string.Concat(tag.Trim().Where(character => !char.IsWhiteSpace(character)));
    }

    private static string NormalizeTagIdentity(string? tag)
    {
        return NormalizeTagDisplayName(tag).ToUpperInvariant();
    }

    private sealed record TagValue(string DisplayName, string NormalizedName);
}
