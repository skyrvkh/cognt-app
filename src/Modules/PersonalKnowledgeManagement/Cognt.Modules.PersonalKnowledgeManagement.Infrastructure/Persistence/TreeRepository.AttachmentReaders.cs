using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    private static IReadOnlyList<AttachmentRow> ReadAttachmentRows(SqliteCommand command)
    {
        List<AttachmentRow> rows = [];
        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            rows.Add(ReadAttachmentRow(reader));
        }

        return rows;
    }

    private static AttachmentRow? ReadSingleAttachmentRow(SqliteCommand command)
    {
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadAttachmentRow(reader) : null;
    }

    private static AttachmentRow ReadAttachmentRow(SqliteDataReader reader)
    {
        return new AttachmentRow(
            reader.GetString(0),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetInt64(8),
            reader.GetInt64(9),
            reader.GetInt64(10));
    }

    private static IReadOnlyList<NoteAttachmentRow> ReadNoteAttachmentRows(SqliteCommand command)
    {
        List<NoteAttachmentRow> rows = [];
        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            rows.Add(new NoteAttachmentRow(
                reader.GetString(0),
                reader.GetInt64(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt64(6)));
        }

        return rows;
    }
}
