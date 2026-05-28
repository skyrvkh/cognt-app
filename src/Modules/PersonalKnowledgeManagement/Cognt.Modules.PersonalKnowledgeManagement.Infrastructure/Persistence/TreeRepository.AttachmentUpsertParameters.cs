using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    private const string AttachmentUpsertSql = """
        INSERT INTO attachments (
            id, collection_id, sha256, original_file_name, stored_file_name,
            relative_path, content_type, file_extension, size_bytes,
            created_at, last_referenced_at
        )
        VALUES (
            $id, $collectionId, $sha256, $originalFileName, $storedFileName,
            $relativePath, $contentType, $fileExtension, $sizeBytes,
            $createdAt, $lastReferencedAt
        )
        ON CONFLICT(collection_id, sha256) DO UPDATE SET
            original_file_name = excluded.original_file_name,
            stored_file_name = excluded.stored_file_name,
            relative_path = excluded.relative_path,
            content_type = excluded.content_type,
            file_extension = excluded.file_extension,
            size_bytes = excluded.size_bytes,
            last_referenced_at = excluded.last_referenced_at;
        """;

    private const string NoteAttachmentUpsertSql = """
        INSERT INTO note_attachments (
            id, collection_id, note_item_id, note_document_id,
            attachment_id, display_name, created_at
        )
        VALUES (
            $id, $collectionId, $noteItemId, $noteDocumentId,
            $attachmentId, $displayName, $createdAt
        )
        ON CONFLICT(collection_id, note_item_id, attachment_id) DO UPDATE SET
            note_document_id = excluded.note_document_id,
            display_name = excluded.display_name;
        """;

    private void AddAttachmentUpsertParameters(
        SqliteCommand command,
        AttachmentRow attachment)
    {
        string attachmentId = NormalizeRequiredText(attachment.Id, nameof(attachment.Id));
        string sha256 = NormalizeRequiredText(attachment.Sha256, nameof(attachment.Sha256));
        string originalFileName = NormalizeRequiredText(attachment.OriginalFileName, nameof(attachment.OriginalFileName));
        string storedFileName = NormalizeRequiredText(attachment.StoredFileName, nameof(attachment.StoredFileName));
        string relativePath = NormalizeFileRelPath(attachment.RelativePath);
        string contentType = NormalizeRequiredText(attachment.ContentType, nameof(attachment.ContentType));
        string fileExtension = NormalizeRequiredText(attachment.FileExtension, nameof(attachment.FileExtension));
        command.Parameters.AddWithValue("$id", attachmentId);
        command.Parameters.AddWithValue("$collectionId", LocalCollectionId);
        command.Parameters.AddWithValue("$sha256", sha256);
        command.Parameters.AddWithValue("$originalFileName", originalFileName);
        command.Parameters.AddWithValue("$storedFileName", storedFileName);
        command.Parameters.AddWithValue("$relativePath", relativePath);
        command.Parameters.AddWithValue("$contentType", contentType);
        command.Parameters.AddWithValue("$fileExtension", fileExtension);
        command.Parameters.AddWithValue("$sizeBytes", Math.Max(0, attachment.SizeBytes));
        command.Parameters.AddWithValue("$createdAt", attachment.CreatedAt);
        command.Parameters.AddWithValue("$lastReferencedAt", attachment.LastReferencedAt);
    }

    private void AddNoteAttachmentUpsertParameters(
        SqliteCommand command,
        NoteAttachmentRow noteAttachment)
    {
        string noteAttachmentId = NormalizeRequiredText(noteAttachment.Id, nameof(noteAttachment.Id));
        string noteDocumentId = NormalizeRequiredText(noteAttachment.NoteDocumentId, nameof(noteAttachment.NoteDocumentId));
        string attachmentId = NormalizeRequiredText(noteAttachment.AttachmentId, nameof(noteAttachment.AttachmentId));
        string displayName = NormalizeRequiredText(noteAttachment.DisplayName, nameof(noteAttachment.DisplayName));
        command.Parameters.AddWithValue("$id", noteAttachmentId);
        command.Parameters.AddWithValue("$collectionId", LocalCollectionId);
        command.Parameters.AddWithValue("$noteItemId", noteAttachment.NoteItemId);
        command.Parameters.AddWithValue("$noteDocumentId", noteDocumentId);
        command.Parameters.AddWithValue("$attachmentId", attachmentId);
        command.Parameters.AddWithValue("$displayName", displayName);
        command.Parameters.AddWithValue("$createdAt", noteAttachment.CreatedAt);
    }
}
