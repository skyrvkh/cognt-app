using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    private static long[] CreateDistinctPositiveIds(IReadOnlyList<long> cardIds)
    {
        return cardIds.Where(static cardId => cardId > 0).Distinct().ToArray();
    }

    private static SqliteCommand CreateLoadCardsByIdsCommand(
        SqliteConnection connection,
        IReadOnlyList<long> cardIds,
        int offset,
        int count)
    {
        SqliteCommand command = connection.CreateCommand();
        string[] parameterNames = CreateParameterNames(count);
        command.CommandText = $"""
            SELECT {FlashcardRecordProjection}
            FROM flashcard_card
            WHERE is_deleted = 0
              AND back_rel_path IS NULL
              AND id IN ({string.Join(", ", parameterNames)});
            """;

        for (int i = 0; i < count; i++)
        {
            command.Parameters.AddWithValue(parameterNames[i], cardIds[offset + i]);
        }

        return command;
    }

    private static string[] CreateParameterNames(int count)
    {
        return Enumerable.Range(0, count)
            .Select(static index => string.Concat("$id", index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
    }

    private static FlashcardRecord GetCard(
        long cardId,
        SqliteConnection connection,
        SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = CreateGetCardCommand(connection, cardId);
        command.Transaction = transaction;
        return ReadRequiredCard(command, cardId);
    }

    private static SqliteCommand CreateGetCardCommand(SqliteConnection connection, long cardId)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {FlashcardRecordProjection}
            FROM flashcard_card
            WHERE id = $id
              AND is_deleted = 0
              AND back_rel_path IS NULL
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", cardId);
        return command;
    }

    private static FlashcardRecord ReadRequiredCard(SqliteCommand command, long cardId)
    {
        using SqliteDataReader reader = command.ExecuteReader();
        if (reader.Read())
        {
            return ReadCard(reader);
        }

        throw new InvalidOperationException($"Flashcard {cardId} does not exist.");
    }

    private static IReadOnlyList<FlashcardRecord> ReadCards(SqliteCommand command)
    {
        List<FlashcardRecord> cards = [];
        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            cards.Add(ReadCard(reader));
        }

        return cards;
    }

    private static FlashcardRecord ReadCard(SqliteDataReader reader)
    {
        return new FlashcardRecord(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)),
            (FlashcardLearningState)reader.GetInt32(4));
    }
}
