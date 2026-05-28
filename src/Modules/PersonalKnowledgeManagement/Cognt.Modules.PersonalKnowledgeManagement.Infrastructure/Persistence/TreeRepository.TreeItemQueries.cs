using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    IReadOnlyList<KnowledgeTreeItemRow> ITreeRepository.LoadRootItems()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, uid, parent_id, kind, title, position, file_rel_path, child_count,
                       is_deleted, created_at, updated_at, file_size, file_mtime_utc, content_hash
                FROM item
                WHERE parent_id IS NULL AND is_deleted = 0
                ORDER BY position, id;
                """;

            return ReadRows(command);
        });
    }

    IReadOnlyList<KnowledgeTreeItemRow> ITreeRepository.LoadChildren(long parentId)
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, uid, parent_id, kind, title, position, file_rel_path, child_count,
                       is_deleted, created_at, updated_at, file_size, file_mtime_utc, content_hash
                FROM item
                WHERE parent_id = $parentId AND is_deleted = 0
                ORDER BY position, id;
                """;
            command.Parameters.AddWithValue("$parentId", parentId);

            return ReadRows(command);
        });
    }

    bool ITreeRepository.HasAnyItems()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM item LIMIT 1);";
            return Convert.ToInt64(command.ExecuteScalar()) != 0;
        });
    }

    KnowledgeTreeItemRow? ITreeRepository.GetItem(long itemId)
    {
        return UseConnection(connection =>
        {
            return GetItem(itemId, connection, transaction: null);
        });
    }
}
