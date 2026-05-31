using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    private T UseConnection<T>(
        Func<SqliteConnection, T> action,
        [CallerMemberName] string? operation = null)
    {
        lock (connectionLock)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                return action(GetConnection());
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger?.LogError(
                    ex,
                    "SQLite flashcard repository operation failed. Operation={Operation} DatabasePath={DatabasePath} ElapsedMs={ElapsedMs}",
                    operation,
                    databasePath,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }

    private void UseConnection(
        Action<SqliteConnection> action,
        [CallerMemberName] string? operation = null)
    {
        lock (connectionLock)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                action(GetConnection());
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger?.LogError(
                    ex,
                    "SQLite flashcard repository operation failed. Operation={Operation} DatabasePath={DatabasePath} ElapsedMs={ElapsedMs}",
                    operation,
                    databasePath,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }

    private SqliteConnection GetConnection()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(SqliteFlashcardRepository));
        }

        if (connection is null)
        {
            connection = OpenConnection();
        }

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
