using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    private void EnsureSchema(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS flashcard_deck (
              id INTEGER PRIMARY KEY,
              uid TEXT NOT NULL UNIQUE,
              name TEXT NOT NULL UNIQUE,
              created_at INTEGER NOT NULL,
              updated_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS flashcard_card (
              id INTEGER PRIMARY KEY,
              uid TEXT NOT NULL UNIQUE,
              deck_id INTEGER NOT NULL REFERENCES flashcard_deck(id) ON DELETE CASCADE,
              card_type TEXT NOT NULL,
              front_rel_path TEXT NOT NULL,
              back_rel_path TEXT,
              source_rel_path TEXT,
              source_context_id TEXT,
              source_slot_id TEXT,
              source_target_id TEXT,
              source_snapshot_hash TEXT,
              cloze_index INTEGER,
              fsrs_card_state_json TEXT NOT NULL,
              due_at INTEGER NOT NULL,
              state INTEGER NOT NULL,
              stability REAL,
              difficulty REAL,
              fsrs_step INTEGER,
              last_review_at INTEGER,
              elapsed_days INTEGER NOT NULL DEFAULT 0,
              scheduled_days INTEGER NOT NULL DEFAULT 0,
              reps INTEGER NOT NULL DEFAULT 0,
              lapses INTEGER NOT NULL DEFAULT 0,
              is_suspended INTEGER NOT NULL DEFAULT 0,
              suspended_at INTEGER,
              is_deleted INTEGER NOT NULL DEFAULT 0,
              created_at INTEGER NOT NULL,
              updated_at INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_flashcard_card_due
            ON flashcard_card(is_deleted, due_at, id);

            CREATE INDEX IF NOT EXISTS idx_flashcard_card_deck
            ON flashcard_card(deck_id, is_deleted, due_at);

            CREATE TABLE IF NOT EXISTS flashcard_review_history (
              id INTEGER PRIMARY KEY,
              uid TEXT NOT NULL UNIQUE,
              card_id INTEGER NOT NULL REFERENCES flashcard_card(id) ON DELETE CASCADE,
              reviewed_at INTEGER NOT NULL,
              rating INTEGER NOT NULL,
              previous_due_at INTEGER NOT NULL,
              next_due_at INTEGER NOT NULL,
              previous_state INTEGER NOT NULL,
              next_state INTEGER NOT NULL,
              review_log_json TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_flashcard_review_history_card
            ON flashcard_review_history(card_id, reviewed_at);

            CREATE INDEX IF NOT EXISTS idx_flashcard_review_history_reviewed_at
            ON flashcard_review_history(reviewed_at, card_id);

            CREATE TABLE IF NOT EXISTS flashcard_review_settings (
              id INTEGER PRIMARY KEY CHECK (id = 1),
              max_due_review_cards_per_day INTEGER NOT NULL,
              max_new_cards_per_day INTEGER NOT NULL,
              desired_retention REAL NOT NULL DEFAULT 0.9,
              learning_steps TEXT NOT NULL DEFAULT '60,600',
              relearning_steps TEXT NOT NULL DEFAULT '600',
              fsrs_optimization_enabled INTEGER NOT NULL DEFAULT 0,
              fsrs_parameters TEXT,
              is_cram_enabled INTEGER NOT NULL DEFAULT 0,
              show_flashcards_in_navigation_tree INTEGER NOT NULL DEFAULT 0,
              updated_at INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();

        EnsureCompatibilityColumns(connection);

        ExecutePragma(
            connection,
            """
            DROP INDEX IF EXISTS ux_flashcard_card_source_prompt_group_active;
            """);

        ExecutePragma(
            connection,
            """
            CREATE UNIQUE INDEX IF NOT EXISTS ux_flashcard_card_source_context_slot_active
            ON flashcard_card(source_rel_path, source_context_id, source_slot_id)
            WHERE is_deleted = 0
              AND source_context_id IS NOT NULL
              AND source_slot_id IS NOT NULL;
            """);

        ExecutePragma(
            connection,
            """
            CREATE INDEX IF NOT EXISTS idx_flashcard_card_review_queue
            ON flashcard_card(is_deleted, is_suspended, state, due_at, id);
            """);
    }

    private void ExecutePragma(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
