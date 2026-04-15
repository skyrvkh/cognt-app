using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    private long InsertTreeItemRecord(
        long? parentId,
        KnowledgeTreeItemKind kind,
        string title,
        long position,
        string? fileRelPath,
        long now,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO item (
                uid, parent_id, kind, title, position, file_rel_path,
                child_count, is_deleted, created_at, updated_at
            )
            VALUES (
                $uid, $parentId, $kind, $title, $position, $fileRelPath,
                0, 0, $createdAt, $updatedAt
            )
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$uid", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$parentId", ToDbParameterValue(parentId));
        command.Parameters.AddWithValue("$kind", (int)kind);
        command.Parameters.AddWithValue("$title", NormalizeTitle(title));
        command.Parameters.AddWithValue("$position", position);
        command.Parameters.AddWithValue("$fileRelPath", ToDbParameterValue(fileRelPath));
        command.Parameters.AddWithValue("$createdAt", now);
        command.Parameters.AddWithValue("$updatedAt", now);

        return Convert.ToInt64(command.ExecuteScalar());
    }
}
