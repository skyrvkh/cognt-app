using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    IReadOnlyList<KnowledgeTreeItemRow> ITreeRepository.LoadNoteItems()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, uid, parent_id, kind, title, position, file_rel_path, child_count,
                       is_deleted, created_at, updated_at, file_size, file_mtime_utc, content_hash
                FROM item
                WHERE kind = 2
                  AND file_rel_path IS NOT NULL
                  AND is_deleted = 0
                ORDER BY title COLLATE NOCASE, position, id;
                """;

            return ReadRows(command);
        });
    }

    IReadOnlyList<KnowledgeTreeItemRow> ITreeRepository.SearchNoteItems(string query, int limit)
    {
        string normalizedQuery = query.Trim();
        int boundedLimit = Math.Clamp(limit, 1, 64);
        string escapedQuery = EscapeLikePattern(normalizedQuery);

        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, uid, parent_id, kind, title, position, file_rel_path, child_count,
                       is_deleted, created_at, updated_at, file_size, file_mtime_utc, content_hash
                FROM item
                WHERE kind = 2
                  AND file_rel_path IS NOT NULL
                  AND is_deleted = 0
                  AND (
                    $isEmpty = 1
                    OR title COLLATE NOCASE LIKE $contains ESCAPE '\'
                    OR file_rel_path COLLATE NOCASE LIKE $contains ESCAPE '\'
                  )
                ORDER BY
                  CASE
                    WHEN $isEmpty = 0 AND title COLLATE NOCASE = $query THEN 0
                    WHEN $isEmpty = 0 AND title COLLATE NOCASE LIKE $prefix ESCAPE '\' THEN 1
                    WHEN $isEmpty = 0 AND file_rel_path COLLATE NOCASE LIKE $prefix ESCAPE '\' THEN 2
                    WHEN $isEmpty = 0 THEN 3
                    ELSE 4
                  END,
                  title COLLATE NOCASE,
                  file_rel_path COLLATE NOCASE,
                  id
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$query", normalizedQuery);
            command.Parameters.AddWithValue("$prefix", $"{escapedQuery}%");
            command.Parameters.AddWithValue("$contains", $"%{escapedQuery}%");
            command.Parameters.AddWithValue("$isEmpty", normalizedQuery.Length == 0 ? 1 : 0);
            command.Parameters.AddWithValue("$limit", boundedLimit);

            return ReadRows(command);
        });
    }
}
