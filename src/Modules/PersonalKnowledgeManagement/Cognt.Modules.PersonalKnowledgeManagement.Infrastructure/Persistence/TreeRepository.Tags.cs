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
            for (int offset = 0; offset < ids.Length; offset += 400)
            {
                int count = Math.Min(400, ids.Length - offset);
                using SqliteCommand command = connection.CreateCommand();
                string[] parameterNames = CreateParameterNames(count, "$id");
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
                AddIdParameters(command, parameterNames, ids, offset, count);

                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    expanded.Add(reader.GetInt64(0));
                }
            }

            return SortDistinctIds(expanded);
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
            for (int offset = 0; offset < ids.Length; offset += 400)
            {
                int count = Math.Min(400, ids.Length - offset);
                using SqliteCommand command = connection.CreateCommand();
                string[] parameterNames = CreateParameterNames(count, "$id");
                command.CommandText = $"""
                    SELECT it.item_id, t.display_name, t.normalized_name
                    FROM item_tag it
                    JOIN tag t ON t.id = it.tag_id
                    JOIN item i ON i.id = it.item_id
                    WHERE i.is_deleted = 0
                      AND it.item_id IN ({string.Join(", ", parameterNames)})
                    ORDER BY t.display_name COLLATE NOCASE, t.id;
                    """;
                AddIdParameters(command, parameterNames, ids, offset, count);

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
            IReadOnlyList<long> existingItemIds = LoadExistingItemIds(connection, ids, transaction);

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
        string[] tags = NormalizeTagIdentities(normalizedTags);
        if (ids.Length == 0 || tags.Length == 0)
        {
            return;
        }

        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            long now = GetCurrentUnixTimeSeconds();
            IReadOnlyList<long> existingItemIds = LoadExistingItemIds(connection, ids, transaction);

            for (int itemOffset = 0; itemOffset < existingItemIds.Count; itemOffset += 400)
            {
                int itemCount = Math.Min(400, existingItemIds.Count - itemOffset);
                for (int tagOffset = 0; tagOffset < tags.Length; tagOffset += 400)
                {
                    int tagCount = Math.Min(400, tags.Length - tagOffset);
                    using SqliteCommand command = connection.CreateCommand();
                    command.Transaction = transaction;
                    string[] itemParameterNames = CreateParameterNames(itemCount, "$item");
                    string[] tagParameterNames = CreateParameterNames(tagCount, "$tag");
                    command.CommandText = $"""
                        DELETE FROM item_tag
                        WHERE item_id IN ({string.Join(", ", itemParameterNames)})
                          AND tag_id IN (
                            SELECT id
                            FROM tag
                            WHERE normalized_name IN ({string.Join(", ", tagParameterNames)})
                          );
                        """;
                    AddIdParameters(command, itemParameterNames, existingItemIds, itemOffset, itemCount);
                    AddStringParameters(command, tagParameterNames, tags, tagOffset, tagCount);

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
            AddIdParameters(command, parameterNames, itemIds, offset, count);

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                existing.Add(reader.GetInt64(0));
            }
        }

        return SortDistinctIds(existing);
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
            AddIdParameters(command, parameterNames, itemIds, offset, count);
            command.ExecuteNonQuery();
        }
    }

    private static string[] CreateParameterNames(int count, string prefix)
    {
        string[] parameterNames = new string[count];
        for (int index = 0; index < count; index++)
        {
            parameterNames[index] = string.Concat(prefix, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return parameterNames;
    }

    private static void AddIdParameters(
        SqliteCommand command,
        IReadOnlyList<string> parameterNames,
        IReadOnlyList<long> values,
        int offset,
        int count)
    {
        for (int i = 0; i < count; i++)
        {
            command.Parameters.AddWithValue(parameterNames[i], values[offset + i]);
        }
    }

    private static void AddStringParameters(
        SqliteCommand command,
        IReadOnlyList<string> parameterNames,
        IReadOnlyList<string> values,
        int offset,
        int count)
    {
        for (int i = 0; i < count; i++)
        {
            command.Parameters.AddWithValue(parameterNames[i], values[offset + i]);
        }
    }

    private static long[] NormalizeItemIds(IReadOnlyList<long> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);

        if (itemIds.Count == 0)
        {
            return [];
        }

        HashSet<long> seenIds = [];
        List<long> normalizedIds = new(itemIds.Count);
        for (int index = 0; index < itemIds.Count; index++)
        {
            long itemId = itemIds[index];
            if (itemId > 0 && seenIds.Add(itemId))
            {
                normalizedIds.Add(itemId);
            }
        }

        normalizedIds.Sort();
        return normalizedIds.Count == 0 ? [] : normalizedIds.ToArray();
    }

    private static TagValue[] NormalizeTags(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (tags.Count == 0)
        {
            return [];
        }

        Dictionary<string, TagValue> normalizedByIdentity = new(StringComparer.Ordinal);
        for (int index = 0; index < tags.Count; index++)
        {
            string displayName = NormalizeTagDisplayName(tags[index]);
            if (displayName.Length == 0)
            {
                continue;
            }

            string normalizedName = displayName.ToUpperInvariant();
            normalizedByIdentity.TryAdd(normalizedName, new TagValue(displayName, normalizedName));
        }

        if (normalizedByIdentity.Count == 0)
        {
            return [];
        }

        TagValue[] normalizedTags = new TagValue[normalizedByIdentity.Count];
        int tagIndex = 0;
        foreach (TagValue tag in normalizedByIdentity.Values)
        {
            normalizedTags[tagIndex++] = tag;
        }

        Array.Sort(normalizedTags, static (left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.DisplayName, right.DisplayName));
        return normalizedTags;
    }

    private static string NormalizeTagDisplayName(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> trimmed = tag.AsSpan().Trim();
        char[] characters = new char[trimmed.Length];
        int length = 0;
        for (int index = 0; index < trimmed.Length; index++)
        {
            char character = trimmed[index];
            if (!char.IsWhiteSpace(character))
            {
                characters[length++] = character;
            }
        }

        return length == 0 ? string.Empty : new string(characters, 0, length);
    }

    private static string NormalizeTagIdentity(string? tag)
    {
        return NormalizeTagDisplayName(tag).ToUpperInvariant();
    }

    private static string[] NormalizeTagIdentities(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (tags.Count == 0)
        {
            return [];
        }

        HashSet<string> seenTags = new(StringComparer.Ordinal);
        List<string> normalizedTags = new(tags.Count);
        for (int index = 0; index < tags.Count; index++)
        {
            string normalizedTag = NormalizeTagIdentity(tags[index]);
            if (normalizedTag.Length > 0 && seenTags.Add(normalizedTag))
            {
                normalizedTags.Add(normalizedTag);
            }
        }

        return normalizedTags.Count == 0 ? [] : normalizedTags.ToArray();
    }

    private static long[] SortDistinctIds(List<long> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return [];
        }

        itemIds.Sort();

        int distinctCount = 0;
        long previousId = 0;
        bool hasPreviousId = false;
        for (int index = 0; index < itemIds.Count; index++)
        {
            long itemId = itemIds[index];
            if (hasPreviousId && itemId == previousId)
            {
                continue;
            }

            itemIds[distinctCount++] = itemId;
            previousId = itemId;
            hasPreviousId = true;
        }

        long[] distinctIds = new long[distinctCount];
        for (int index = 0; index < distinctCount; index++)
        {
            distinctIds[index] = itemIds[index];
        }

        return distinctIds;
    }

    private sealed record TagValue(string DisplayName, string NormalizedName);
}
