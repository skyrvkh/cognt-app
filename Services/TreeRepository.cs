using CogntApp.Models.Tree;
using Microsoft.Data.Sqlite;
using System.IO;

namespace CogntApp.Services;

public interface ITreeRepository : IDisposable
{
    IReadOnlyList<TreeItemRow> LoadRootItems();

    IReadOnlyList<TreeItemRow> LoadChildren(long parentId);

    IReadOnlyList<TreeItemRow> LoadNoteItems();

    IReadOnlyList<TreeItemRow> SearchNoteItems(string query, int limit);

    long CreateFolder(long? parentId, string title, long? previousSiblingId = null, long? nextSiblingId = null);

    long CreateNote(long? parentId, string title, string fileRelPath, long? previousSiblingId = null, long? nextSiblingId = null);

    void MoveItem(long itemId, long? newParentId, long? previousSiblingId, long? nextSiblingId);

    void ReorderItem(long itemId, long? previousSiblingId, long? nextSiblingId);

    void RenumberSiblings(long? parentId);

    bool WouldCreateCycle(long movingItemId, long? newParentId);

    bool HasAnyItems();

    TreeItemRow? GetItem(long itemId);

    TreeItemRow? FindNoteByFileRelPath(string fileRelPath);

    void RenameItem(long itemId, string title);

    void DeleteItem(long itemId);

}

public sealed class SqliteTreeRepository : ITreeRepository
{
    private const long PositionGap = 1000;

    private readonly object connectionLock = new();
    private readonly string databasePath;
    private SqliteConnection? connection;
    private bool disposed;

    public SqliteTreeRepository(string databasePath)
    {
        this.databasePath = string.IsNullOrWhiteSpace(databasePath)
            ? throw new ArgumentException("Database path is required.", nameof(databasePath))
            : databasePath;
    }

    public IReadOnlyList<TreeItemRow> LoadRootItems()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, uid, parent_id, kind, title, position, file_rel_path, child_count,
                       is_deleted, created_at, updated_at, file_size, file_mtime_utc, content_hash
                FROM item
                WHERE parent_id IS NULL AND is_deleted = 0
                ORDER BY position, id;
                """;

            return ReadRows(command);
        });
    }

    public IReadOnlyList<TreeItemRow> LoadChildren(long parentId)
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, uid, parent_id, kind, title, position, file_rel_path, child_count,
                       is_deleted, created_at, updated_at, file_size, file_mtime_utc, content_hash
                FROM item
                WHERE parent_id = $parentId AND is_deleted = 0
                ORDER BY position, id;
                """;
            command.Parameters.AddWithValue("$parentId", parentId);

            return ReadRows(command);
        });
    }

    public IReadOnlyList<TreeItemRow> LoadNoteItems()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, uid, parent_id, kind, title, position, file_rel_path, child_count,
                       is_deleted, created_at, updated_at, file_size, file_mtime_utc, content_hash
                FROM item
                WHERE kind = 2
                  AND file_rel_path IS NOT NULL
                  AND is_deleted = 0
                ORDER BY title COLLATE NOCASE, position, id;
                """;

            return ReadRows(command);
        });
    }

    public IReadOnlyList<TreeItemRow> SearchNoteItems(string query, int limit)
    {
        string normalizedQuery = query.Trim();
        int boundedLimit = Math.Clamp(limit, 1, 64);
        string escapedQuery = EscapeLikePattern(normalizedQuery);

        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, uid, parent_id, kind, title, position, file_rel_path, child_count,
                       is_deleted, created_at, updated_at, file_size, file_mtime_utc, content_hash
                FROM item
                WHERE kind = 2
                  AND file_rel_path IS NOT NULL
                  AND is_deleted = 0
                  AND (
                    $isEmpty = 1
                    OR title COLLATE NOCASE LIKE $contains ESCAPE '\'
                    OR file_rel_path COLLATE NOCASE LIKE $contains ESCAPE '\'
                  )
                ORDER BY
                  CASE
                    WHEN $isEmpty = 0 AND title COLLATE NOCASE = $query THEN 0
                    WHEN $isEmpty = 0 AND title COLLATE NOCASE LIKE $prefix ESCAPE '\' THEN 1
                    WHEN $isEmpty = 0 AND file_rel_path COLLATE NOCASE LIKE $prefix ESCAPE '\' THEN 2
                    WHEN $isEmpty = 0 THEN 3
                    ELSE 4
                  END,
                  title COLLATE NOCASE,
                  file_rel_path COLLATE NOCASE,
                  id
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$query", normalizedQuery);
            command.Parameters.AddWithValue("$prefix", $"{escapedQuery}%");
            command.Parameters.AddWithValue("$contains", $"%{escapedQuery}%");
            command.Parameters.AddWithValue("$isEmpty", normalizedQuery.Length == 0 ? 1 : 0);
            command.Parameters.AddWithValue("$limit", boundedLimit);

            return ReadRows(command);
        });
    }

    public long CreateFolder(long? parentId, string title, long? previousSiblingId = null, long? nextSiblingId = null)
    {
        return UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();

            EnsureValidParent(parentId, connection, transaction);
            long now = NowUnixTime();
            long position = AllocatePosition(parentId, previousSiblingId, nextSiblingId, connection, transaction);
            long id = InsertItem(
                parentId,
                TreeItemKind.Folder,
                title,
                position,
                fileRelPath: null,
                now,
                connection,
                transaction);

            RecalculateChildCount(parentId, connection, transaction);
            transaction.Commit();
            return id;
        });
    }

    public long CreateNote(
        long? parentId,
        string title,
        string fileRelPath,
        long? previousSiblingId = null,
        long? nextSiblingId = null)
    {
        if (string.IsNullOrWhiteSpace(fileRelPath))
        {
            throw new ArgumentException("File relative path is required.", nameof(fileRelPath));
        }

        return UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();

            EnsureValidParent(parentId, connection, transaction);
            long now = NowUnixTime();
            long position = AllocatePosition(parentId, previousSiblingId, nextSiblingId, connection, transaction);
            long id = InsertItem(
                parentId,
                TreeItemKind.Note,
                title,
                position,
                NormalizeFileRelPath(fileRelPath),
                now,
                connection,
                transaction);

            RecalculateChildCount(parentId, connection, transaction);
            transaction.Commit();
            return id;
        });
    }

    public void MoveItem(long itemId, long? newParentId, long? previousSiblingId, long? nextSiblingId)
    {
        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();

            TreeItemRow item = GetRequiredItem(itemId, connection, transaction);

            if (WouldCreateCycle(itemId, newParentId, connection, transaction))
            {
                throw new InvalidOperationException("A tree item cannot be moved under itself or one of its descendants.");
            }

            EnsureValidParent(newParentId, connection, transaction);
            long newPosition = AllocatePosition(
                newParentId,
                previousSiblingId,
                nextSiblingId,
                connection,
                transaction,
                excludedItemId: itemId);
            long now = NowUnixTime();

            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE item
                SET parent_id = $parentId,
                    position = $position,
                    updated_at = $updatedAt
                WHERE id = $id AND is_deleted = 0;
                """;
            command.Parameters.AddWithValue("$parentId", DbValue(newParentId));
            command.Parameters.AddWithValue("$position", newPosition);
            command.Parameters.AddWithValue("$updatedAt", now);
            command.Parameters.AddWithValue("$id", itemId);
            command.ExecuteNonQuery();

            if (item.ParentId != newParentId)
            {
                RecalculateChildCount(item.ParentId, connection, transaction);
                RecalculateChildCount(newParentId, connection, transaction);
            }

            transaction.Commit();
        });
    }

    public void ReorderItem(long itemId, long? previousSiblingId, long? nextSiblingId)
    {
        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();

            TreeItemRow item = GetRequiredItem(itemId, connection, transaction);
            long position = AllocatePosition(
                item.ParentId,
                previousSiblingId,
                nextSiblingId,
                connection,
                transaction,
                excludedItemId: itemId);
            long now = NowUnixTime();

            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE item
                SET position = $position,
                    updated_at = $updatedAt
                WHERE id = $id AND is_deleted = 0;
                """;
            command.Parameters.AddWithValue("$position", position);
            command.Parameters.AddWithValue("$updatedAt", now);
            command.Parameters.AddWithValue("$id", itemId);
            command.ExecuteNonQuery();

            transaction.Commit();
        });
    }

    public void RenumberSiblings(long? parentId)
    {
        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            RenumberSiblings(parentId, connection, transaction);
            transaction.Commit();
        });
    }

    public bool WouldCreateCycle(long movingItemId, long? newParentId)
    {
        return UseConnection(connection => WouldCreateCycle(movingItemId, newParentId, connection, transaction: null));
    }

    public bool HasAnyItems()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM item LIMIT 1);";
            return Convert.ToInt64(command.ExecuteScalar()) != 0;
        });
    }

    public TreeItemRow? GetItem(long itemId)
    {
        return UseConnection(connection => GetItem(itemId, connection, transaction: null));
    }

    public TreeItemRow? FindNoteByFileRelPath(string fileRelPath)
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, uid, parent_id, kind, title, position, file_rel_path, child_count,
                       is_deleted, created_at, updated_at, file_size, file_mtime_utc, content_hash
                FROM item
                WHERE kind = 2
                  AND file_rel_path = $fileRelPath
                  AND is_deleted = 0
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$fileRelPath", NormalizeFileRelPath(fileRelPath));

            return ReadSingleRow(command);
        });
    }

    public void RenameItem(long itemId, string title)
    {
        UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE item
                SET title = $title,
                    updated_at = $updatedAt
                WHERE id = $id
                  AND is_deleted = 0;
                """;
            command.Parameters.AddWithValue("$title", NormalizeTitle(title));
            command.Parameters.AddWithValue("$updatedAt", NowUnixTime());
            command.Parameters.AddWithValue("$id", itemId);

            if (command.ExecuteNonQuery() == 0)
            {
                throw new InvalidOperationException($"Tree item {itemId} does not exist.");
            }
        });
    }

    public void DeleteItem(long itemId)
    {
        UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            TreeItemRow item = GetRequiredItem(itemId, connection, transaction);

            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                WITH RECURSIVE deleted_items(id) AS (
                  SELECT id
                  FROM item
                  WHERE id = $id

                  UNION ALL

                  SELECT i.id
                  FROM item i
                  JOIN deleted_items d ON i.parent_id = d.id
                )
                UPDATE item
                SET is_deleted = 1,
                    updated_at = $updatedAt
                WHERE id IN (SELECT id FROM deleted_items);
                """;
            command.Parameters.AddWithValue("$id", itemId);
            command.Parameters.AddWithValue("$updatedAt", NowUnixTime());
            command.ExecuteNonQuery();

            RecalculateChildCount(item.ParentId, connection, transaction);
            transaction.Commit();
        });
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
        EnsureSchema(connection);
        return connection;
    }

    private static void ExecutePragma(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS item (
              id INTEGER PRIMARY KEY,
              uid TEXT NOT NULL UNIQUE,
              parent_id INTEGER REFERENCES item(id) ON DELETE CASCADE,

              kind INTEGER NOT NULL,

              title TEXT NOT NULL,
              position INTEGER NOT NULL,

              file_rel_path TEXT,

              child_count INTEGER NOT NULL DEFAULT 0,

              file_size INTEGER,
              file_mtime_utc INTEGER,
              content_hash TEXT,

              is_deleted INTEGER NOT NULL DEFAULT 0,

              created_at INTEGER NOT NULL,
              updated_at INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_item_parent_position
            ON item(parent_id, position, id);

            CREATE INDEX IF NOT EXISTS idx_item_uid
            ON item(uid);

            CREATE INDEX IF NOT EXISTS idx_item_kind_title
            ON item(kind, title COLLATE NOCASE, id);

            CREATE INDEX IF NOT EXISTS idx_item_file_rel_path
            ON item(file_rel_path);

            """;
        command.ExecuteNonQuery();
    }

    private long InsertItem(
        long? parentId,
        TreeItemKind kind,
        string title,
        long position,
        string? fileRelPath,
        long now,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO item (
                uid, parent_id, kind, title, position, file_rel_path,
                child_count, is_deleted, created_at, updated_at
            )
            VALUES (
                $uid, $parentId, $kind, $title, $position, $fileRelPath,
                0, 0, $createdAt, $updatedAt
            )
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$uid", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$parentId", DbValue(parentId));
        command.Parameters.AddWithValue("$kind", (int)kind);
        command.Parameters.AddWithValue("$title", NormalizeTitle(title));
        command.Parameters.AddWithValue("$position", position);
        command.Parameters.AddWithValue("$fileRelPath", DbValue(fileRelPath));
        command.Parameters.AddWithValue("$createdAt", now);
        command.Parameters.AddWithValue("$updatedAt", now);

        return Convert.ToInt64(command.ExecuteScalar());
    }

    private long AllocatePosition(
        long? parentId,
        long? previousSiblingId,
        long? nextSiblingId,
        SqliteConnection connection,
        SqliteTransaction transaction,
        long? excludedItemId = null,
        bool retried = false)
    {
        long? previousPosition = previousSiblingId.HasValue && previousSiblingId != excludedItemId
            ? GetSiblingPosition(previousSiblingId.Value, parentId, connection, transaction)
            : null;
        long? nextPosition = nextSiblingId.HasValue && nextSiblingId != excludedItemId
            ? GetSiblingPosition(nextSiblingId.Value, parentId, connection, transaction)
            : null;

        if (previousPosition.HasValue && nextPosition.HasValue)
        {
            if (nextPosition.Value > previousPosition.Value
                && nextPosition.Value - previousPosition.Value > 1)
            {
                return previousPosition.Value + ((nextPosition.Value - previousPosition.Value) / 2);
            }

            if (retried)
            {
                throw new InvalidOperationException("Could not allocate a tree position between the selected siblings.");
            }

            RenumberSiblings(parentId, connection, transaction);
            return AllocatePosition(
                parentId,
                previousSiblingId,
                nextSiblingId,
                connection,
                transaction,
                excludedItemId,
                retried: true);
        }

        if (previousPosition.HasValue)
        {
            return previousPosition.Value + PositionGap;
        }

        if (nextPosition.HasValue)
        {
            if (nextPosition.Value > 1)
            {
                return nextPosition.Value / 2;
            }

            if (retried)
            {
                throw new InvalidOperationException("Could not allocate a tree position before the selected sibling.");
            }

            RenumberSiblings(parentId, connection, transaction);
            return AllocatePosition(
                parentId,
                previousSiblingId,
                nextSiblingId,
                connection,
                transaction,
                excludedItemId,
                retried: true);
        }

        long? maxPosition = GetMaxSiblingPosition(parentId, connection, transaction, excludedItemId);
        return maxPosition.HasValue ? maxPosition.Value + PositionGap : PositionGap;
    }

    private static long GetSiblingPosition(
        long siblingId,
        long? parentId,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT position
            FROM item
            WHERE id = $id
              AND parent_id IS $parentId
              AND is_deleted = 0;
            """;
        command.Parameters.AddWithValue("$id", siblingId);
        command.Parameters.AddWithValue("$parentId", DbValue(parentId));

        object? value = command.ExecuteScalar();
        if (value is null || value == DBNull.Value)
        {
            throw new InvalidOperationException("The drop sibling is not under the target parent.");
        }

        return Convert.ToInt64(value);
    }

    private static long? GetMaxSiblingPosition(
        long? parentId,
        SqliteConnection connection,
        SqliteTransaction transaction,
        long? excludedItemId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT MAX(position)
            FROM item
            WHERE parent_id IS $parentId
              AND is_deleted = 0
              AND ($excludedItemId IS NULL OR id != $excludedItemId);
            """;
        command.Parameters.AddWithValue("$parentId", DbValue(parentId));
        command.Parameters.AddWithValue("$excludedItemId", DbValue(excludedItemId));

        object? value = command.ExecuteScalar();
        return value is null || value == DBNull.Value ? null : Convert.ToInt64(value);
    }

    private static void RenumberSiblings(
        long? parentId,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        List<long> itemIds = [];

        using (SqliteCommand selectCommand = connection.CreateCommand())
        {
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = """
                SELECT id
                FROM item
                WHERE parent_id IS $parentId
                  AND is_deleted = 0
                ORDER BY position, id;
                """;
            selectCommand.Parameters.AddWithValue("$parentId", DbValue(parentId));

            using SqliteDataReader reader = selectCommand.ExecuteReader();
            while (reader.Read())
            {
                itemIds.Add(reader.GetInt64(0));
            }
        }

        long position = PositionGap;
        long now = NowUnixTime();

        foreach (long id in itemIds)
        {
            using SqliteCommand updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = """
                UPDATE item
                SET position = $position,
                    updated_at = $updatedAt
                WHERE id = $id;
                """;
            updateCommand.Parameters.AddWithValue("$position", position);
            updateCommand.Parameters.AddWithValue("$updatedAt", now);
            updateCommand.Parameters.AddWithValue("$id", id);
            updateCommand.ExecuteNonQuery();
            position += PositionGap;
        }
    }

    private bool WouldCreateCycle(
        long movingItemId,
        long? newParentId,
        SqliteConnection connection,
        SqliteTransaction? transaction)
    {
        if (!newParentId.HasValue)
        {
            return false;
        }

        if (newParentId.Value == movingItemId)
        {
            return true;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            WITH RECURSIVE descendants(id) AS (
              SELECT id
              FROM item
              WHERE parent_id = $movingItemId
                AND is_deleted = 0

              UNION ALL

              SELECT i.id
              FROM item i
              JOIN descendants d ON i.parent_id = d.id
              WHERE i.is_deleted = 0
            )
            SELECT EXISTS (
              SELECT 1 FROM descendants WHERE id = $newParentId
            );
            """;
        command.Parameters.AddWithValue("$movingItemId", movingItemId);
        command.Parameters.AddWithValue("$newParentId", newParentId.Value);

        return Convert.ToInt64(command.ExecuteScalar()) != 0;
    }

    private static void EnsureValidParent(
        long? parentId,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        if (!parentId.HasValue)
        {
            return;
        }

        _ = GetRequiredItem(parentId.Value, connection, transaction);
    }

    private static void RecalculateChildCount(
        long? parentId,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        if (!parentId.HasValue)
        {
            return;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE item
            SET child_count = (
                SELECT COUNT(*)
                FROM item child
                WHERE child.parent_id = $parentId
                  AND child.is_deleted = 0
            ),
            updated_at = $updatedAt
            WHERE id = $parentId
              AND is_deleted = 0;
            """;
        command.Parameters.AddWithValue("$parentId", parentId.Value);
        command.Parameters.AddWithValue("$updatedAt", NowUnixTime());
        command.ExecuteNonQuery();
    }

    private static TreeItemRow GetRequiredItem(
        long itemId,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        TreeItemRow? item = GetItem(itemId, connection, transaction);
        return item ?? throw new InvalidOperationException($"Tree item {itemId} does not exist.");
    }

    private static TreeItemRow? GetItem(
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

    private static IReadOnlyList<TreeItemRow> ReadRows(SqliteCommand command)
    {
        List<TreeItemRow> rows = [];
        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            rows.Add(ReadRow(reader));
        }

        return rows;
    }

    private static TreeItemRow? ReadSingleRow(SqliteCommand command)
    {
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadRow(reader) : null;
    }

    private static TreeItemRow ReadRow(SqliteDataReader reader)
    {
        return new TreeItemRow(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetInt64(2),
            (TreeItemKind)reader.GetInt32(3),
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

    private static string NormalizeTitle(string title)
    {
        string normalized = title.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException("Title is required.", nameof(title))
            : normalized;
    }

    private static string NormalizeFileRelPath(string fileRelPath)
    {
        return fileRelPath.Replace('\\', '/').Trim();
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }

    private static object DbValue(long? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private static long NowUnixTime()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
