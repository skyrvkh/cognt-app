using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    KnowledgeTreeItemRow? ITreeRepository.FindNoteByFileRelPath(string fileRelPath)
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, uid, parent_id, kind, title, position, file_rel_path, child_count,
                       is_deleted, created_at, updated_at, file_size, file_mtime_utc, content_hash
                FROM item
                WHERE kind = 2
                  AND file_rel_path = $fileRelPath
                  AND is_deleted = 0
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$fileRelPath", NormalizeFileRelPath(fileRelPath));

            return ReadSingleRow(command);
        });
    }

    KnowledgeTreeItemRow? ITreeRepository.FindItemByFileRelPath(
        string fileRelPath,
        long? parentId,
        KnowledgeTreeItemKind kind,
        bool includeDeleted)
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, uid, parent_id, kind, title, position, file_rel_path, child_count,
                       is_deleted, created_at, updated_at, file_size, file_mtime_utc, content_hash
                FROM item
                WHERE kind = $kind
                  AND file_rel_path = $fileRelPath
                  AND parent_id IS $parentId
                  AND ($includeDeleted = 1 OR is_deleted = 0)
                ORDER BY is_deleted, id
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$kind", (int)kind);
            command.Parameters.AddWithValue("$fileRelPath", NormalizeFileRelPath(fileRelPath));
            command.Parameters.AddWithValue("$parentId", ToDbParameterValue(parentId));
            command.Parameters.AddWithValue("$includeDeleted", includeDeleted ? 1 : 0);

            return ReadSingleRow(command);
        });
    }
}
