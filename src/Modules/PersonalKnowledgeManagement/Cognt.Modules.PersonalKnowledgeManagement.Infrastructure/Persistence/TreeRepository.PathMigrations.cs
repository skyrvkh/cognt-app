using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    void ITreeRepository.RewriteFileRelPathPrefix(string oldPrefix, string newPrefix)
    {
        string normalizedOldPrefix = NormalizeFileRelPath(oldPrefix);
        string normalizedNewPrefix = NormalizeFileRelPath(newPrefix);
        if (normalizedOldPrefix.Length == 0 || normalizedNewPrefix.Length == 0)
        {
            logger?.LogWarning(
                "Rejected tree item file path prefix rewrite because a prefix was empty. OldPrefixLength={OldPrefixLength} NewPrefixLength={NewPrefixLength}",
                normalizedOldPrefix.Length,
                normalizedNewPrefix.Length);
            throw new ArgumentException("Path prefixes must not be empty.");
        }

        UseConnection(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE item
                SET file_rel_path = $newPrefix || substr(file_rel_path, length($oldPrefix) + 1),
                    updated_at = $updatedAt
                WHERE file_rel_path IS NOT NULL
                  AND file_rel_path COLLATE NOCASE LIKE $oldPrefixLike ESCAPE '\';
                """;
            command.Parameters.AddWithValue("$oldPrefix", normalizedOldPrefix);
            command.Parameters.AddWithValue("$newPrefix", normalizedNewPrefix);
            command.Parameters.AddWithValue("$oldPrefixLike", $"{EscapeLikePattern(normalizedOldPrefix)}%");
            command.Parameters.AddWithValue("$updatedAt", GetCurrentUnixTimeSeconds());
            int affectedRows = command.ExecuteNonQuery();
            logger?.LogInformation(
                "Rewrote tree item file path prefix. OldPrefix={OldPrefix} NewPrefix={NewPrefix} AffectedRows={AffectedRows}",
                normalizedOldPrefix,
                normalizedNewPrefix,
                affectedRows);
        });
    }
}
