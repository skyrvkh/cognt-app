using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    bool ITreeRepository.HasDocumentMentions()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT EXISTS (
                  SELECT 1
                  FROM document_mentions
                  WHERE collection_id = $collectionId
                  LIMIT 1
                );
                """;
            command.Parameters.AddWithValue("$collectionId", LocalCollectionId);
            return Convert.ToInt64(command.ExecuteScalar()) != 0;
        });
    }

    IReadOnlyList<DocumentMentionRow> ITreeRepository.LoadDocumentMentionsBySource(string sourceDocumentId)
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, collection_id, source_document_id, source_block_id,
                       target_document_id, mention_type, mention_text, normalized_mention_text,
                       start_offset, end_offset, preview_text, context_before, context_after,
                       status, created_at, updated_at
                FROM document_mentions
                WHERE collection_id = $collectionId
                  AND source_document_id = $sourceDocumentId
                ORDER BY source_block_id, start_offset, id;
                """;
            command.Parameters.AddWithValue("$collectionId", LocalCollectionId);
            command.Parameters.AddWithValue("$sourceDocumentId", NormalizeDocumentId(sourceDocumentId));

            return ReadDocumentMentionRows(command);
        });
    }

    IReadOnlyList<DocumentMentionRow> ITreeRepository.LoadDocumentMentionsByTarget(string targetDocumentId)
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, collection_id, source_document_id, source_block_id,
                       target_document_id, mention_type, mention_text, normalized_mention_text,
                       start_offset, end_offset, preview_text, context_before, context_after,
                       status, created_at, updated_at
                FROM document_mentions
                WHERE collection_id = $collectionId
                  AND target_document_id = $targetDocumentId
                ORDER BY updated_at DESC, source_document_id, source_block_id, start_offset, id;
                """;
            command.Parameters.AddWithValue("$collectionId", LocalCollectionId);
            command.Parameters.AddWithValue("$targetDocumentId", NormalizeDocumentId(targetDocumentId));

            return ReadDocumentMentionRows(command);
        });
    }

    IReadOnlyList<DocumentMentionRow> ITreeRepository.LoadResolvedDocumentMentionsByDocument(string documentId)
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, collection_id, source_document_id, source_block_id,
                       target_document_id, mention_type, mention_text, normalized_mention_text,
                       start_offset, end_offset, preview_text, context_before, context_after,
                       status, created_at, updated_at
                FROM document_mentions
                WHERE collection_id = $collectionId
                  AND status = 'resolved'
                  AND target_document_id IS NOT NULL
                  AND (
                    source_document_id = $documentId
                    OR target_document_id = $documentId
                  )
                ORDER BY updated_at DESC, source_document_id, target_document_id, source_block_id, start_offset, id;
                """;
            command.Parameters.AddWithValue("$collectionId", LocalCollectionId);
            command.Parameters.AddWithValue("$documentId", NormalizeDocumentId(documentId));

            return ReadDocumentMentionRows(command);
        });
    }

}
