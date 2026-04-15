using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    private static bool HasColumn(
        SqliteConnection connection,
        string tableName,
        string columnName)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureColumn(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        using SqliteCommand checkCommand = connection.CreateCommand();
        checkCommand.CommandText = $"PRAGMA table_info({tableName});";

        using (SqliteDataReader reader = checkCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        using SqliteCommand alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        alterCommand.ExecuteNonQuery();
    }
}
