using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    private static IReadOnlyList<DocumentMentionRow> ReadDocumentMentionRows(SqliteCommand command)
    {
        List<DocumentMentionRow> rows = [];
        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            rows.Add(new DocumentMentionRow(
                reader.GetString(0),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.GetString(13),
                reader.GetInt64(14),
                reader.GetInt64(15)));
        }

        return rows;
    }
}
