using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal long EnsureDeck(string name)
    {
        string normalizedName = NormalizeName(name);

        return UseConnection(connection => EnsureDeck(connection, null, normalizedName));
    }

    internal int DeleteDecksReassignCards(
        IReadOnlyList<long> deckIds,
        string fallbackDeckName)
    {
        ArgumentNullException.ThrowIfNull(deckIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackDeckName);

        long[] normalizedDeckIds = deckIds
            .Where(deckId => deckId > 0)
            .Distinct()
            .OrderBy(static deckId => deckId)
            .ToArray();
        if (normalizedDeckIds.Length == 0)
        {
            return 0;
        }

        string normalizedFallbackDeckName = NormalizeName(fallbackDeckName);

        return UseConnection(connection =>
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            long fallbackDeckId = EnsureDeck(connection, transaction, normalizedFallbackDeckName);

            long[] deckIdsToDelete = normalizedDeckIds
                .Where(deckId => deckId != fallbackDeckId)
                .ToArray();
            if (deckIdsToDelete.Length == 0)
            {
                transaction.Commit();
                return 0;
            }

            string[] parameterNames = CreateDeckParameterNames(deckIdsToDelete.Length);
            string deckFilter = string.Join(", ", parameterNames);
            long updatedAt = NowUnixTime();

            using SqliteCommand reassignCardsCommand = connection.CreateCommand();
            reassignCardsCommand.Transaction = transaction;
            reassignCardsCommand.CommandText = $"""
                UPDATE flashcard_card
                SET deck_id = $fallbackDeckId,
                    updated_at = $updatedAt
                WHERE deck_id IN ({deckFilter});
                """;
            reassignCardsCommand.Parameters.AddWithValue("$fallbackDeckId", fallbackDeckId);
            reassignCardsCommand.Parameters.AddWithValue("$updatedAt", updatedAt);
            AddDeckIdParameters(reassignCardsCommand, parameterNames, deckIdsToDelete);
            reassignCardsCommand.ExecuteNonQuery();

            using SqliteCommand deleteDecksCommand = connection.CreateCommand();
            deleteDecksCommand.Transaction = transaction;
            deleteDecksCommand.CommandText = $"""
                DELETE FROM flashcard_deck
                WHERE id IN ({deckFilter})
                  AND id <> $fallbackDeckId;
                """;
            deleteDecksCommand.Parameters.AddWithValue("$fallbackDeckId", fallbackDeckId);
            AddDeckIdParameters(deleteDecksCommand, parameterNames, deckIdsToDelete);
            int deletedDecks = deleteDecksCommand.ExecuteNonQuery();

            transaction.Commit();
            return deletedDecks;
        });
    }

    private static long EnsureDeck(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string normalizedName)
    {
        using SqliteCommand insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            INSERT INTO flashcard_deck (uid, name, created_at, updated_at)
            VALUES ($uid, $name, $now, $now)
            ON CONFLICT(name) DO UPDATE SET updated_at = excluded.updated_at
            RETURNING id;
            """;
        insertCommand.Parameters.AddWithValue("$uid", Guid.NewGuid().ToString("N"));
        insertCommand.Parameters.AddWithValue("$name", normalizedName);
        insertCommand.Parameters.AddWithValue("$now", NowUnixTime());

        return Convert.ToInt64(insertCommand.ExecuteScalar());
    }

    private static void AddDeckIdParameters(
        SqliteCommand command,
        IReadOnlyList<string> parameterNames,
        IReadOnlyList<long> deckIds)
    {
        for (int index = 0; index < deckIds.Count; index++)
        {
            command.Parameters.AddWithValue(parameterNames[index], deckIds[index]);
        }
    }
}
