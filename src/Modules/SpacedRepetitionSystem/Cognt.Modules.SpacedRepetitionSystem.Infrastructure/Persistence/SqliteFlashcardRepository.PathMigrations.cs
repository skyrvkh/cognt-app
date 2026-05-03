using Microsoft.Data.Sqlite;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

internal sealed partial class SqliteFlashcardRepository
{
    internal void RewriteCardFrontRelPathPrefix(string oldPrefix, string newPrefix)
    {
        RewriteNullableCardRelPathPrefix("front_rel_path", oldPrefix, newPrefix);
    }

    internal void RewriteCardSourceRelPathPrefix(string oldPrefix, string newPrefix)
    {
        RewriteNullableCardRelPathPrefix("source_rel_path", oldPrefix, newPrefix);
    }

    private void RewriteNullableCardRelPathPrefix(string columnName, string oldPrefix, string newPrefix)
    {
        string normalizedOldPrefix = NormalizeRelPath(oldPrefix);
        string normalizedNewPrefix = NormalizeRelPath(newPrefix);
        if (normalizedOldPrefix.Length == 0 || normalizedNewPrefix.Length == 0)
        {
            throw new ArgumentException("Path prefixes must not be empty.");
        }

        UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                UPDATE flashcard_card
                SET {columnName} = $newPrefix || substr({columnName}, length($oldPrefix) + 1),
                    updated_at = $updatedAt
                WHERE {columnName} IS NOT NULL
                  AND {columnName} COLLATE NOCASE LIKE $oldPrefixLike ESCAPE '\';
                """;
            command.Parameters.AddWithValue("$oldPrefix", normalizedOldPrefix);
            command.Parameters.AddWithValue("$newPrefix", normalizedNewPrefix);
            command.Parameters.AddWithValue("$oldPrefixLike", $"{EscapeLikePattern(normalizedOldPrefix)}%");
            command.Parameters.AddWithValue("$updatedAt", NowUnixTime());
            command.ExecuteNonQuery();
        });
    }
}
