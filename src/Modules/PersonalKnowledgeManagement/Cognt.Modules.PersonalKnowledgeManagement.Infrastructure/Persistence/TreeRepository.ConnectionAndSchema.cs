using Microsoft.Data.Sqlite;
using System.IO;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    void IDisposable.Dispose()
    {
        lock (connectionLock)
        {
            if (disposed)
            {
                return;
            }

            connection?.Dispose();
            connection = null;
            disposed = true;
        }
    }

    private T UseConnection<T>(Func<SqliteConnection, T> action)
    {
        lock (connectionLock)
        {
            return action(GetConnection());
        }
    }

    private void UseConnection(Action<SqliteConnection> action)
    {
        lock (connectionLock)
        {
            action(GetConnection());
        }
    }

    private SqliteConnection GetConnection()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(SqliteTreeRepository));
        }

        connection ??= OpenConnection();
        return connection;
    }

    private SqliteConnection OpenConnection()
    {
        string? directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        SqliteConnectionStringBuilder connectionString = new()
        {
            DataSource = databasePath,
            Pooling = false,
        };
        SqliteConnection connection = new(connectionString.ToString());
        connection.Open();
        ExecutePragma(connection, "PRAGMA foreign_keys = ON;");
        ExecutePragma(connection, "PRAGMA journal_mode = WAL;");
        ExecutePragma(connection, "PRAGMA synchronous = NORMAL;");
        EnsureSchemaInitialized(connection);
        return connection;
    }

    private static void ExecutePragma(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
