using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal FlashcardReviewSettingsReadModel LoadReviewSettings()
    {
        return UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT max_due_review_cards_per_day,
                       max_new_cards_per_day,
                       desired_retention,
                       learning_steps,
                       relearning_steps,
                       fsrs_optimization_enabled,
                       fsrs_parameters,
                       is_cram_enabled,
                       show_flashcards_in_navigation_tree
                FROM flashcard_review_settings
                WHERE id = 1;
                """;

            using SqliteDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadReviewSettings(reader);
            }

            return FlashcardReviewSettingsReadModel.Default;
        });
    }

    internal void SaveReviewSettings(FlashcardReviewSettingsReadModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        FlashcardReviewSettingsReadModel normalized = settings.Normalize();

        UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO flashcard_review_settings (
                    id, max_due_review_cards_per_day, max_new_cards_per_day, desired_retention, learning_steps, relearning_steps, fsrs_optimization_enabled, fsrs_parameters, is_cram_enabled, show_flashcards_in_navigation_tree, updated_at
                )
                VALUES (
                    1, $reviewLimit, $newLimit, $desiredRetention, $learningSteps, $relearningSteps, $optimizationEnabled, $fsrsParameters, $cramEnabled, $showFlashcardsInNavigationTree, $updatedAt
                )
                ON CONFLICT(id) DO UPDATE SET
                    max_due_review_cards_per_day = excluded.max_due_review_cards_per_day,
                    max_new_cards_per_day = excluded.max_new_cards_per_day,
                    desired_retention = excluded.desired_retention,
                    learning_steps = excluded.learning_steps,
                    relearning_steps = excluded.relearning_steps,
                    fsrs_optimization_enabled = excluded.fsrs_optimization_enabled,
                    fsrs_parameters = excluded.fsrs_parameters,
                    is_cram_enabled = excluded.is_cram_enabled,
                    show_flashcards_in_navigation_tree = excluded.show_flashcards_in_navigation_tree,
                    updated_at = excluded.updated_at;
                """;
            long updatedAt = NowUnixTime();
            AddReviewSettingsParameters(command, normalized, updatedAt);
            command.ExecuteNonQuery();
        });
    }

    private static FlashcardReviewSettingsReadModel ReadReviewSettings(SqliteDataReader reader)
    {
        return new FlashcardReviewSettingsReadModel(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetDouble(2),
            FlashcardReviewStepStorage.Deserialize(
                reader.IsDBNull(3) ? null : reader.GetString(3),
                FlashcardReviewSettingsReadModel.DefaultLearningSteps),
            FlashcardReviewStepStorage.Deserialize(
                reader.IsDBNull(4) ? null : reader.GetString(4),
                FlashcardReviewSettingsReadModel.DefaultRelearningSteps),
            reader.GetInt32(5) != 0,
            FlashcardReviewParameterStorage.Deserialize(
                reader.IsDBNull(6) ? null : reader.GetString(6)),
            reader.GetInt32(7) != 0,
            reader.GetInt32(8) != 0).Normalize();
    }

    private static void AddReviewSettingsParameters(
        SqliteCommand command,
        FlashcardReviewSettingsReadModel settings,
        long updatedAt)
    {
        FlashcardReviewSettingsReadModel normalized = settings.Normalize();
        command.Parameters.AddWithValue("$reviewLimit", normalized.MaximumReviewCardsPerDay);
        command.Parameters.AddWithValue("$newLimit", normalized.MaximumNewCardsPerDay);
        command.Parameters.AddWithValue("$desiredRetention", normalized.DesiredRetention);
        command.Parameters.AddWithValue("$learningSteps", FlashcardReviewStepStorage.Serialize(
            normalized.LearningSteps,
            FlashcardReviewSettingsReadModel.DefaultLearningSteps));
        command.Parameters.AddWithValue("$relearningSteps", FlashcardReviewStepStorage.Serialize(
            normalized.RelearningSteps,
            FlashcardReviewSettingsReadModel.DefaultRelearningSteps));
        command.Parameters.AddWithValue("$optimizationEnabled", normalized.IsOptimizationEnabled ? 1 : 0);
        command.Parameters.AddWithValue(
            "$fsrsParameters",
            DbValue(FlashcardReviewParameterStorage.Serialize(normalized.FsrsParameters)));
        command.Parameters.AddWithValue("$cramEnabled", normalized.IsCramEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$showFlashcardsInNavigationTree", normalized.ShowFlashcardsInNavigationTree ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", updatedAt);
    }
}
