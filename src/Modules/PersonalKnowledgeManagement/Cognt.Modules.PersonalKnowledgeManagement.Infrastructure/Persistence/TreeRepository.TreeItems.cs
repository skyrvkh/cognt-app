using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    void ITreeRepository.RenameItem(long itemId, string title)
    {
        string normalizedTitle = NormalizeTitle(title);
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
            command.Parameters.AddWithValue("$title", normalizedTitle);
            command.Parameters.AddWithValue("$updatedAt", GetCurrentUnixTimeSeconds());
            command.Parameters.AddWithValue("$id", itemId);

            if (command.ExecuteNonQuery() == 0)
            {
                logger?.LogWarning(
                    "Tree item rename failed because the item was not found. ItemId={ItemId}",
                    itemId);
                throw new InvalidOperationException($"Tree item {itemId} does not exist.");
            }
        });
    }

    void ITreeRepository.DeleteItem(long itemId)
    {
        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            KnowledgeTreeItemRow item = GetRequiredItem(itemId, connection, transaction, logger);

            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            long updatedAt = GetCurrentUnixTimeSeconds();

            using SqliteCommand deleteSourceMentionsCommand = connection.CreateCommand();
            deleteSourceMentionsCommand.Transaction = transaction;
            deleteSourceMentionsCommand.CommandText = """
                WITH RECURSIVE deleted_items(id, uid) AS (
                  SELECT id, uid
                  FROM item
                  WHERE id = $id

                  UNION ALL

                  SELECT i.id, i.uid
                  FROM item i
                  JOIN deleted_items d ON i.parent_id = d.id
                )
                DELETE FROM document_mentions
                WHERE collection_id = $collectionId
                  AND source_document_id IN (SELECT uid FROM deleted_items);
                """;
            deleteSourceMentionsCommand.Parameters.AddWithValue("$id", itemId);
            deleteSourceMentionsCommand.Parameters.AddWithValue("$collectionId", LocalCollectionId);
            deleteSourceMentionsCommand.ExecuteNonQuery();

            using SqliteCommand unlinkTargetMentionsCommand = connection.CreateCommand();
            unlinkTargetMentionsCommand.Transaction = transaction;
            unlinkTargetMentionsCommand.CommandText = """
                WITH RECURSIVE deleted_items(id, uid) AS (
                  SELECT id, uid
                  FROM item
                  WHERE id = $id

                  UNION ALL

                  SELECT i.id, i.uid
                  FROM item i
                  JOIN deleted_items d ON i.parent_id = d.id
                )
                UPDATE document_mentions
                SET target_document_id = NULL,
                    status = 'unresolved',
                    updated_at = $updatedAt
                WHERE collection_id = $collectionId
                  AND target_document_id IN (SELECT uid FROM deleted_items);
                """;
            unlinkTargetMentionsCommand.Parameters.AddWithValue("$id", itemId);
            unlinkTargetMentionsCommand.Parameters.AddWithValue("$collectionId", LocalCollectionId);
            unlinkTargetMentionsCommand.Parameters.AddWithValue("$updatedAt", updatedAt);
            unlinkTargetMentionsCommand.ExecuteNonQuery();

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
            command.Parameters.AddWithValue("$updatedAt", updatedAt);
            command.ExecuteNonQuery();

            RecalculateChildCount(item.ParentId, connection, transaction);
            transaction.Commit();
        });
    }
}
