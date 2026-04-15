using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    void ITreeRepository.RenameItem(long itemId, string title)
    {
        UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE item
                SET title = $title,
                    updated_at = $updatedAt
                WHERE id = $id
                  AND is_deleted = 0;
                """;
            command.Parameters.AddWithValue("$title", NormalizeTitle(title));
            command.Parameters.AddWithValue("$updatedAt", GetCurrentUnixTimeSeconds());
            command.Parameters.AddWithValue("$id", itemId);

            if (command.ExecuteNonQuery() == 0)
            {
                throw new InvalidOperationException($"Tree item {itemId} does not exist.");
            }
        });
    }

    void ITreeRepository.DeleteItem(long itemId)
    {
        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            KnowledgeTreeItemRow item = GetRequiredItem(itemId, connection, transaction);

            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                WITH RECURSIVE deleted_items(id) AS (
                  SELECT id
                  FROM item
                  WHERE id = $id

                  UNION ALL

                  SELECT i.id
                  FROM item i
                  JOIN deleted_items d ON i.parent_id = d.id
                )
                UPDATE item
                SET is_deleted = 1,
                    updated_at = $updatedAt
                WHERE id IN (SELECT id FROM deleted_items);
                """;
            command.Parameters.AddWithValue("$id", itemId);
            command.Parameters.AddWithValue("$updatedAt", GetCurrentUnixTimeSeconds());
            command.ExecuteNonQuery();

            RecalculateChildCount(item.ParentId, connection, transaction);
            transaction.Commit();
        });
    }
}
