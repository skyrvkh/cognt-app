using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
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
            throw new ObjectDisposedException(nameof(SqliteFlashcardRepository));
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
        EnsureSchema(connection);
        return connection;
    }

    public void Dispose()
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
}
