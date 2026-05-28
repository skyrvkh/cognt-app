using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    IReadOnlyList<AttachmentRow> ITreeRepository.LoadAttachments()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, collection_id, sha256, original_file_name, stored_file_name,
                       relative_path, content_type, file_extension, size_bytes, created_at,
                       last_referenced_at
                FROM attachments
                WHERE collection_id = $collectionId
                ORDER BY last_referenced_at DESC, original_file_name COLLATE NOCASE, id;
                """;
            command.Parameters.AddWithValue("$collectionId", LocalCollectionId);

            return ReadAttachmentRows(command);
        });
    }

    IReadOnlyList<NoteAttachmentRow> ITreeRepository.LoadNoteAttachments()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, collection_id, note_item_id, note_document_id,
                       attachment_id, display_name, created_at
                FROM note_attachments
                WHERE collection_id = $collectionId
                ORDER BY created_at DESC, note_item_id, attachment_id;
                """;
            command.Parameters.AddWithValue("$collectionId", LocalCollectionId);

            return ReadNoteAttachmentRows(command);
        });
    }

    AttachmentRow? ITreeRepository.GetAttachment(string attachmentId)
    {
        string normalizedAttachmentId = NormalizeRequiredText(attachmentId, nameof(attachmentId));
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, collection_id, sha256, original_file_name, stored_file_name,
                       relative_path, content_type, file_extension, size_bytes,
                       created_at, last_referenced_at
                FROM attachments
                WHERE collection_id = $collectionId
                  AND id = $id
                LIMIT 1;
            """;
            command.Parameters.AddWithValue("$collectionId", LocalCollectionId);
            command.Parameters.AddWithValue("$id", normalizedAttachmentId);

            return ReadSingleAttachmentRow(command);
        });
    }

    AttachmentRow? ITreeRepository.FindAttachmentBySha256(string sha256)
    {
        string normalizedSha256 = NormalizeRequiredText(sha256, nameof(sha256));
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, collection_id, sha256, original_file_name, stored_file_name,
                       relative_path, content_type, file_extension, size_bytes,
                       created_at, last_referenced_at
                FROM attachments
                WHERE collection_id = $collectionId
                  AND sha256 = $sha256
                LIMIT 1;
            """;
            command.Parameters.AddWithValue("$collectionId", LocalCollectionId);
            command.Parameters.AddWithValue("$sha256", normalizedSha256);

            return ReadSingleAttachmentRow(command);
        });
    }

}
