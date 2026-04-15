using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    private static long GetSiblingPosition(
        long siblingId,
        long? parentId,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT position
            FROM item
            WHERE id = $id
              AND parent_id IS $parentId
              AND is_deleted = 0;
            """;
        command.Parameters.AddWithValue("$id", siblingId);
        command.Parameters.AddWithValue("$parentId", ToDbParameterValue(parentId));

        object? value = command.ExecuteScalar();
        if (value is null || value == DBNull.Value)
        {
            throw new InvalidOperationException("The drop sibling is not under the target parent.");
        }

        return Convert.ToInt64(value);
    }

    private static long? GetMaxSiblingPosition(
        long? parentId,
        SqliteConnection connection,
        SqliteTransaction transaction,
        long? excludedItemId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT MAX(position)
            FROM item
            WHERE parent_id IS $parentId
              AND is_deleted = 0
              AND ($excludedItemId IS NULL OR id != $excludedItemId);
            """;
        command.Parameters.AddWithValue("$parentId", ToDbParameterValue(parentId));
        command.Parameters.AddWithValue("$excludedItemId", ToDbParameterValue(excludedItemId));

        object? value = command.ExecuteScalar();
        return value is null || value == DBNull.Value ? null : Convert.ToInt64(value);
    }

    private static void RenumberSiblings(
        long? parentId,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        List<long> itemIds = [];

        using (SqliteCommand selectCommand = connection.CreateCommand())
        {
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = """
                SELECT id
                FROM item
                WHERE parent_id IS $parentId
                  AND is_deleted = 0
                ORDER BY position, id;
                """;
            selectCommand.Parameters.AddWithValue("$parentId", ToDbParameterValue(parentId));

            using SqliteDataReader reader = selectCommand.ExecuteReader();
            while (reader.Read())
            {
                itemIds.Add(reader.GetInt64(0));
            }
        }

        long position = PositionGap;
        long now = GetCurrentUnixTimeSeconds();

        foreach (long id in itemIds)
        {
            using SqliteCommand updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = """
                UPDATE item
                SET position = $position,
                    updated_at = $updatedAt
                WHERE id = $id;
                """;
            updateCommand.Parameters.AddWithValue("$position", position);
            updateCommand.Parameters.AddWithValue("$updatedAt", now);
            updateCommand.Parameters.AddWithValue("$id", id);
            updateCommand.ExecuteNonQuery();
            position += PositionGap;
        }
    }
}
