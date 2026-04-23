using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository : ITreeRepository
{
    private const string LocalCollectionId = "default";
    private const long PositionGap = 1000;

    private readonly object connectionLock = new();
    private readonly string databasePath;
    private SqliteConnection? connection;
    private bool disposed;

    internal SqliteTreeRepository(string databasePath)
    {
        this.databasePath = string.IsNullOrWhiteSpace(databasePath)
            ? throw new ArgumentException("Database path is required.", nameof(databasePath))
            : databasePath;
    }

    private bool WouldCreateCycle(
        long movingItemId,
        long? newParentId,
        SqliteConnection connection,
        SqliteTransaction? transaction)
    {
        if (!newParentId.HasValue)
        {
            return false;
        }

        if (newParentId.Value == movingItemId)
        {
            return true;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            WITH RECURSIVE descendants(id) AS (
              SELECT id
              FROM item
              WHERE parent_id = $movingItemId
                AND is_deleted = 0

              UNION ALL

              SELECT i.id
              FROM item i
              JOIN descendants d ON i.parent_id = d.id
              WHERE i.is_deleted = 0
            )
            SELECT EXISTS (
              SELECT 1 FROM descendants WHERE id = $newParentId
            );
            """;
        command.Parameters.AddWithValue("$movingItemId", movingItemId);
        command.Parameters.AddWithValue("$newParentId", newParentId.Value);

        return Convert.ToInt64(command.ExecuteScalar()) != 0;
    }

    private static void EnsureValidParent(
        long? parentId,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        if (!parentId.HasValue)
        {
            return;
        }

        _ = GetRequiredItem(parentId.Value, connection, transaction);
    }

    private static void RecalculateChildCount(
        long? parentId,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        if (!parentId.HasValue)
        {
            return;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE item
            SET child_count = (
                SELECT COUNT(*)
                FROM item child
                WHERE child.parent_id = $parentId
                  AND child.is_deleted = 0
            ),
            updated_at = $updatedAt
            WHERE id = $parentId
              AND is_deleted = 0;
            """;
        command.Parameters.AddWithValue("$parentId", parentId.Value);
        command.Parameters.AddWithValue("$updatedAt", GetCurrentUnixTimeSeconds());
        command.ExecuteNonQuery();
    }
}
