using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository : IDisposable
{
    private const string FlashcardRecordProjection = """
        id, front_rel_path, fsrs_card_state_json, due_at, state
        """;

    private readonly object connectionLock = new();
    private readonly string databasePath;
    private SqliteConnection? connection;
    private bool disposed;

    internal SqliteFlashcardRepository(string databasePath)
    {
        this.databasePath = string.IsNullOrWhiteSpace(databasePath)
            ? throw new ArgumentException("Database path is required.", nameof(databasePath))
            : databasePath;
    }

}
