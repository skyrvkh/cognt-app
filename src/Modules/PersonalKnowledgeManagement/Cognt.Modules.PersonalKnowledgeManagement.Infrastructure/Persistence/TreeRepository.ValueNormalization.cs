namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    private static string NormalizeTitle(string title)
    {
        string normalized = title.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException("Title is required.", nameof(title))
            : normalized;
    }

    private static string NormalizeFileRelPath(string fileRelPath)
    {
        return fileRelPath.Replace('\\', '/').Trim();
    }

    private static string NormalizeDocumentId(string documentId)
    {
        return NormalizeRequiredText(documentId, nameof(documentId));
    }

    private static string NormalizeMentionId(string mentionId)
    {
        return NormalizeRequiredText(mentionId, nameof(mentionId));
    }

    private static string NormalizeSourceBlockId(string sourceBlockId)
    {
        return NormalizeRequiredText(sourceBlockId, nameof(sourceBlockId));
    }

    private static string NormalizeRequiredText(string value, string parameterName)
    {
        string normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException($"{parameterName} is required.", parameterName)
            : normalized;
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }

    private static object ToDbParameterValue(long? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object ToDbParameterValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private static long GetCurrentUnixTimeSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
