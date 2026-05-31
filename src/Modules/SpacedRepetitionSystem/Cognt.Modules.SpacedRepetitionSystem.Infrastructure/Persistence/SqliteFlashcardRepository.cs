using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository : IDisposable
{
    private const int NewStateValue = (int)FlashcardLearningState.New;
    private const int LearningStateValue = (int)FlashcardLearningState.Learning;
    private const int ReviewStateValue = (int)FlashcardLearningState.Review;
    private const int RelearningStateValue = (int)FlashcardLearningState.Relearning;

    private const string ActiveSingleFileCardPredicate = """
        is_deleted = 0
          AND is_suspended = 0
          AND back_rel_path IS NULL
        """;

    private const string FlashcardRecordProjection = """
        id, front_rel_path, fsrs_card_state_json, due_at, state
        """;

    private readonly object connectionLock = new();
    private readonly string databasePath;
    private readonly ILogger? logger;
    private SqliteConnection? connection;
    private bool disposed;

    internal SqliteFlashcardRepository(string databasePath, ILogger? logger = null)
    {
        this.databasePath = string.IsNullOrWhiteSpace(databasePath)
            ? throw new ArgumentException("Database path is required.", nameof(databasePath))
            : databasePath;
        this.logger = logger;
    }

}
