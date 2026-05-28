using Microsoft.Data.Sqlite;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Tags;

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
                AddParameters(command, parameterNames, ids, offset, count);
                expanded.AddRange(ReadRows(command, static reader => reader.GetInt64(0)));
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
                AddParameters(command, parameterNames, ids, offset, count);
                rows.AddRange(ReadRows(command, static reader => new ItemTagRow(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2))));
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

            return ReadRows(command, static reader => reader.GetString(0));
        });
    }

    void ITreeRepository.AssignTagsToItems(
        IReadOnlyList<long> itemIds,
        IReadOnlyList<string> tags)
    {
        long[] ids = NormalizeItemIds(itemIds);
        KnowledgeTagValue[] normalizedTags = KnowledgeTagNormalizer.NormalizeValues(tags);
        if (ids.Length == 0 || normalizedTags.Length == 0)
        {
            return;
        }

        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            long now = GetCurrentUnixTimeSeconds();
            IReadOnlyList<long> existingItemIds = LoadExistingItemIds(connection, ids, transaction);

            foreach (KnowledgeTagValue tag in normalizedTags)
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
        string[] tags = KnowledgeTagNormalizer.NormalizeIdentities(normalizedTags);
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
                    AddParameters(command, itemParameterNames, existingItemIds, itemOffset, itemCount);
                    AddParameters(command, tagParameterNames, tags, tagOffset, tagCount);

                    command.ExecuteNonQuery();
                }
            }

            TouchItems(connection, transaction, existingItemIds, now);
            transaction.Commit();
        });
    }

}
