using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    void ITreeRepository.MoveItem(long itemId, long? newParentId, long? previousSiblingId, long? nextSiblingId)
    {
        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();

            KnowledgeTreeItemRow item = GetRequiredItem(itemId, connection, transaction, logger);

            if (WouldCreateCycle(itemId, newParentId, connection, transaction))
            {
                logger?.LogWarning(
                    "Rejected tree item move because it would create a cycle. ItemId={ItemId} NewParentId={NewParentId}",
                    itemId,
                    newParentId);
                throw new InvalidOperationException("A tree item cannot be moved under itself or one of its descendants.");
            }

            EnsureValidParent(newParentId, connection, transaction, logger);
            long newPosition = AllocatePosition(
                newParentId,
                previousSiblingId,
                nextSiblingId,
                connection,
                transaction,
                excludedItemId: itemId);
            long now = GetCurrentUnixTimeSeconds();

            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE item
                SET parent_id = $parentId,
                    position = $position,
                    updated_at = $updatedAt
                WHERE id = $id AND is_deleted = 0;
                """;
            command.Parameters.AddWithValue("$parentId", ToDbParameterValue(newParentId));
            command.Parameters.AddWithValue("$position", newPosition);
            command.Parameters.AddWithValue("$updatedAt", now);
            command.Parameters.AddWithValue("$id", itemId);
            command.ExecuteNonQuery();

            if (item.ParentId != newParentId)
            {
                RecalculateChildCount(item.ParentId, connection, transaction);
                RecalculateChildCount(newParentId, connection, transaction);
            }

            transaction.Commit();
        });
    }

    void ITreeRepository.ReorderItem(long itemId, long? previousSiblingId, long? nextSiblingId)
    {
        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();

            KnowledgeTreeItemRow item = GetRequiredItem(itemId, connection, transaction, logger);
            long position = AllocatePosition(
                item.ParentId,
                previousSiblingId,
                nextSiblingId,
                connection,
                transaction,
                excludedItemId: itemId);
            long now = GetCurrentUnixTimeSeconds();

            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE item
                SET position = $position,
                    updated_at = $updatedAt
                WHERE id = $id AND is_deleted = 0;
                """;
            command.Parameters.AddWithValue("$position", position);
            command.Parameters.AddWithValue("$updatedAt", now);
            command.Parameters.AddWithValue("$id", itemId);
            command.ExecuteNonQuery();

            transaction.Commit();
        });
    }
}
