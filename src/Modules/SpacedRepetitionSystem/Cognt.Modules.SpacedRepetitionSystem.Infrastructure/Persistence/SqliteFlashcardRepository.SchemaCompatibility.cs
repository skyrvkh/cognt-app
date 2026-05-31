using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    private static readonly CompatibilityColumn[] CompatibilityColumns =
    [
        new("flashcard_card", "fsrs_step", "INTEGER"),
        new("flashcard_card", "last_review_at", "INTEGER"),
        new("flashcard_card", "elapsed_days", "INTEGER NOT NULL DEFAULT 0"),
        new("flashcard_card", "scheduled_days", "INTEGER NOT NULL DEFAULT 0"),
        new("flashcard_card", "reps", "INTEGER NOT NULL DEFAULT 0"),
        new("flashcard_card", "lapses", "INTEGER NOT NULL DEFAULT 0"),
        new("flashcard_card", "is_suspended", "INTEGER NOT NULL DEFAULT 0"),
        new("flashcard_card", "suspended_at", "INTEGER"),
        new("flashcard_card", "source_context_id", "TEXT"),
        new("flashcard_card", "source_slot_id", "TEXT"),
        new("flashcard_card", "source_target_id", "TEXT"),
        new("flashcard_card", "source_snapshot_hash", "TEXT"),
        new("flashcard_review_settings", "desired_retention", "REAL NOT NULL DEFAULT 0.9"),
        new("flashcard_review_settings", "learning_steps", "TEXT NOT NULL DEFAULT '60,600'"),
        new("flashcard_review_settings", "relearning_steps", "TEXT NOT NULL DEFAULT '600'"),
        new("flashcard_review_settings", "fsrs_optimization_enabled", "INTEGER NOT NULL DEFAULT 0"),
        new("flashcard_review_settings", "fsrs_parameters", "TEXT"),
        new("flashcard_review_settings", "is_cram_enabled", "INTEGER NOT NULL DEFAULT 0"),
        new("flashcard_review_settings", "show_flashcards_in_navigation_tree", "INTEGER NOT NULL DEFAULT 0"),
    ];

    private static void EnsureCompatibilityColumns(SqliteConnection connection)
    {
        foreach (CompatibilityColumn column in CompatibilityColumns)
        {
            EnsureColumn(connection, column.TableName, column.ColumnName, column.Definition);
        }

        BackfillLegacySourceIdentityColumns(connection);
    }

    private static void BackfillLegacySourceIdentityColumns(SqliteConnection connection)
    {
        bool hasLegacyContextColumn = HasColumn(connection, "flashcard_card", "source_prompt_id");
        bool hasLegacySlotColumn = HasColumn(connection, "flashcard_card", "source_group_id");

        if (hasLegacyContextColumn)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE flashcard_card
                SET source_context_id = source_prompt_id
                WHERE source_context_id IS NULL
                  AND source_prompt_id IS NOT NULL;
                """;
            command.ExecuteNonQuery();
        }

        if (hasLegacySlotColumn)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE flashcard_card
                SET source_slot_id = source_group_id
                WHERE source_slot_id IS NULL
                  AND source_group_id IS NOT NULL;
                """;
            command.ExecuteNonQuery();
        }
    }

    private readonly struct CompatibilityColumn
    {
        internal CompatibilityColumn(string tableName, string columnName, string definition)
        {
            TableName = tableName;
            ColumnName = columnName;
            Definition = definition;
        }

        internal string TableName { get; }

        internal string ColumnName { get; }

        internal string Definition { get; }
    }
}
