using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    private const string DeleteDocumentMentionsBySourceSql = """
        DELETE FROM document_mentions
        WHERE collection_id = $collectionId
          AND source_document_id = $sourceDocumentId;
        """;

    private const string InsertDocumentMentionSql = """
        INSERT INTO document_mentions (
            id, collection_id, source_document_id, source_block_id,
            target_document_id, mention_type, mention_text, normalized_mention_text,
            start_offset, end_offset, preview_text, context_before, context_after,
            status, created_at, updated_at
        )
        VALUES (
            $id, $collectionId, $sourceDocumentId, $sourceBlockId,
            $targetDocumentId, $mentionType, $mentionText, $normalizedMentionText,
            $startOffset, $endOffset, $previewText, $contextBefore, $contextAfter,
            $status, $createdAt, $updatedAt
        );
        """;

    private void AddDeleteDocumentMentionsBySourceParameters(
        SqliteCommand command,
        string normalizedSourceDocumentId)
    {
        command.Parameters.AddWithValue("$collectionId", LocalCollectionId);
        command.Parameters.AddWithValue("$sourceDocumentId", normalizedSourceDocumentId);
    }

    private void AddDocumentMentionInsertParameters(
        SqliteCommand command,
        string normalizedSourceDocumentId,
        DocumentMentionRow mention)
    {
        string mentionId = NormalizeMentionId(mention.Id);
        string sourceBlockId = NormalizeSourceBlockId(mention.SourceBlockId);
        string mentionType = NormalizeRequiredText(mention.MentionType, nameof(mention.MentionType));
        string mentionText = NormalizeRequiredText(mention.MentionText, nameof(mention.MentionText));
        string normalizedMentionText = NormalizeRequiredText(mention.NormalizedMentionText, nameof(mention.NormalizedMentionText));
        string previewText = NormalizeRequiredText(mention.PreviewText, nameof(mention.PreviewText));
        string status = NormalizeRequiredText(mention.Status, nameof(mention.Status));
        command.Parameters.AddWithValue("$id", mentionId);
        command.Parameters.AddWithValue("$collectionId", LocalCollectionId);
        command.Parameters.AddWithValue("$sourceDocumentId", normalizedSourceDocumentId);
        command.Parameters.AddWithValue("$sourceBlockId", sourceBlockId);
        command.Parameters.AddWithValue("$targetDocumentId", ToDbParameterValue(mention.TargetDocumentId));
        command.Parameters.AddWithValue("$mentionType", mentionType);
        command.Parameters.AddWithValue("$mentionText", mentionText);
        command.Parameters.AddWithValue("$normalizedMentionText", normalizedMentionText);
        command.Parameters.AddWithValue("$startOffset", Math.Max(0, mention.StartOffset));
        command.Parameters.AddWithValue("$endOffset", Math.Max(Math.Max(0, mention.StartOffset), mention.EndOffset));
        command.Parameters.AddWithValue("$previewText", previewText);
        command.Parameters.AddWithValue("$contextBefore", ToDbParameterValue(mention.ContextBefore));
        command.Parameters.AddWithValue("$contextAfter", ToDbParameterValue(mention.ContextAfter));
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$createdAt", mention.CreatedAt);
        command.Parameters.AddWithValue("$updatedAt", mention.UpdatedAt);
    }
}
