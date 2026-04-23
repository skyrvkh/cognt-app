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
}
