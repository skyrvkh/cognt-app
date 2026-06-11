using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    void ITreeRepository.UpsertAttachment(AttachmentRow attachment)
    {
        UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = AttachmentUpsertSql;
            AddAttachmentUpsertParameters(command, attachment);
            command.ExecuteNonQuery();
        });
    }

    void ITreeRepository.UpsertNoteAttachment(NoteAttachmentRow attachment)
    {
        UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = NoteAttachmentUpsertSql;
            AddNoteAttachmentUpsertParameters(command, attachment);
            command.ExecuteNonQuery();
        });
    }

    void ITreeRepository.ReplaceNoteAttachments(
        long noteItemId,
        IReadOnlyList<NoteAttachmentRow> attachments)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        foreach (NoteAttachmentRow attachment in attachments)
        {
            if (attachment.NoteItemId != noteItemId)
            {
                throw new InvalidOperationException("All replacement note attachments must belong to the target note.");
            }
        }

        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            using SqliteCommand deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = """
                DELETE FROM note_attachments
                WHERE collection_id = $collectionId
                  AND note_item_id = $noteItemId;
                """;
            deleteCommand.Parameters.AddWithValue("$collectionId", LocalCollectionId);
            deleteCommand.Parameters.AddWithValue("$noteItemId", noteItemId);
            deleteCommand.ExecuteNonQuery();

            foreach (NoteAttachmentRow attachment in attachments)
            {
                using SqliteCommand upsertCommand = connection.CreateCommand();
                upsertCommand.Transaction = transaction;
                upsertCommand.CommandText = NoteAttachmentUpsertSql;
                AddNoteAttachmentUpsertParameters(upsertCommand, attachment);
                upsertCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        });
    }
}
