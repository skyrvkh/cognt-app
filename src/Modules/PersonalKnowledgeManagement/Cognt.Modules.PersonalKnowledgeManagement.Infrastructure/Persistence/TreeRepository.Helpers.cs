using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    private static KnowledgeTreeItemRow GetRequiredItem(
        long itemId,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        KnowledgeTreeItemRow? item = GetItem(itemId, connection, transaction);
        return item ?? throw new InvalidOperationException($"Tree item {itemId} does not exist.");
    }

    private static KnowledgeTreeItemRow? GetItem(
        long itemId,
        SqliteConnection connection,
        SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id, uid, parent_id, kind, title, position, file_rel_path, child_count,
                   is_deleted, created_at, updated_at, file_size, file_mtime_utc, content_hash
            FROM item
            WHERE id = $id
              AND is_deleted = 0
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", itemId);

        return ReadSingleRow(command);
    }

    private static IReadOnlyList<KnowledgeTreeItemRow> ReadRows(SqliteCommand command)
    {
        List<KnowledgeTreeItemRow> rows = [];
        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            rows.Add(ReadRow(reader));
        }

        return rows;
    }

    private static KnowledgeTreeItemRow? ReadSingleRow(SqliteCommand command)
    {
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadRow(reader) : null;
    }

    private static KnowledgeTreeItemRow ReadRow(SqliteDataReader reader)
    {
        return new KnowledgeTreeItemRow(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetInt64(2),
            (KnowledgeTreeItemKind)reader.GetInt32(3),
            reader.GetString(4),
            reader.GetInt64(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetInt32(7),
            reader.GetInt32(8) != 0,
            reader.GetInt64(9),
            reader.GetInt64(10),
            reader.IsDBNull(11) ? null : reader.GetInt64(11),
            reader.IsDBNull(12) ? null : reader.GetInt64(12),
            reader.IsDBNull(13) ? null : reader.GetString(13));
    }
}
