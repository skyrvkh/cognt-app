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
                       desired_retention
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

        UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO flashcard_review_settings (
                    id, max_due_review_cards_per_day, max_new_cards_per_day, desired_retention, updated_at
                )
                VALUES (
                    1, $reviewLimit, $newLimit, $desiredRetention, $updatedAt
                )
                ON CONFLICT(id) DO UPDATE SET
                    max_due_review_cards_per_day = excluded.max_due_review_cards_per_day,
                    max_new_cards_per_day = excluded.max_new_cards_per_day,
                    desired_retention = excluded.desired_retention,
                    updated_at = excluded.updated_at;
                """;
            AddReviewSettingsParameters(command, settings, NowUnixTime());
            command.ExecuteNonQuery();
        });
    }

    private static FlashcardReviewSettingsReadModel ReadReviewSettings(SqliteDataReader reader)
    {
        return new FlashcardReviewSettingsReadModel(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetDouble(2)).Normalize();
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
        command.Parameters.AddWithValue("$updatedAt", updatedAt);
    }
}
