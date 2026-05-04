using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal long EnsureDeck(string name)
    {
        string normalizedName = NormalizeName(name);

        return UseConnection(connection =>
        {
            using SqliteCommand insertCommand = connection.CreateCommand();
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
        });
    }

}
