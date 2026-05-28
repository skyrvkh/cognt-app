namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Tags;

internal sealed record KnowledgeTagValue(string DisplayName, string NormalizedName);

internal static partial class KnowledgeTagNormalizer
{
    internal static string[] NormalizeDisplayNames(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (tags.Count == 0)
        {
            return [];
        }

        HashSet<string> seenTags = new(StringComparer.OrdinalIgnoreCase);
        List<string> normalizedTags = [];
        AddDisplayNames(tags, seenTags, normalizedTags);
        normalizedTags.Sort(StringComparer.OrdinalIgnoreCase);
        return normalizedTags.ToArray();
    }

    internal static void AddDisplayNames(
        IEnumerable<string>? source,
        HashSet<string> seenTags,
        List<string> normalizedTags)
    {
        if (source is null)
        {
            return;
        }

        foreach (string tag in source)
        {
            string normalizedTag = NormalizeDisplayName(tag);
            if (normalizedTag.Length > 0 && seenTags.Add(normalizedTag))
            {
                normalizedTags.Add(normalizedTag);
            }
        }
    }

    private static string NormalizeDisplayName(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> trimmed = tag.AsSpan().Trim();
        int firstWhitespaceIndex = -1;
        for (int index = 0; index < trimmed.Length; index++)
        {
            if (char.IsWhiteSpace(trimmed[index]))
            {
                firstWhitespaceIndex = index;
                break;
            }
        }

        if (firstWhitespaceIndex < 0)
        {
            return trimmed.Length == tag.Length ? tag : trimmed.ToString();
        }

        char[] characters = new char[trimmed.Length];
        trimmed[..firstWhitespaceIndex].CopyTo(characters.AsSpan());
        int length = firstWhitespaceIndex;
        for (int index = firstWhitespaceIndex + 1; index < trimmed.Length; index++)
        {
            char character = trimmed[index];
            if (!char.IsWhiteSpace(character))
            {
                characters[length++] = character;
            }
        }

        return length == 0 ? string.Empty : new string(characters, 0, length);
    }
}
