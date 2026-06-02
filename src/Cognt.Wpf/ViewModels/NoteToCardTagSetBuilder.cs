namespace CogntApp.ViewModels;

internal static class NoteToCardTagSetBuilder
{
    internal static string[] CreateAdditions(
        IEnumerable<string> existingTags,
        IEnumerable<string> candidateTags)
    {
        List<string> addedTags = [];
        var knownTags = new HashSet<string>(existingTags, StringComparer.OrdinalIgnoreCase);

        foreach (string value in candidateTags)
        {
            string tag = NormalizeDisplayName(value);
            if (tag.Length == 0 || !knownTags.Add(tag))
            {
                continue;
            }

            addedTags.Add(tag);
        }

        return addedTags.ToArray();
    }

    internal static string NormalizeDisplayName(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> trimmedTag = tag.Trim();
        Span<char> normalized = trimmedTag.Length <= 256
            ? stackalloc char[trimmedTag.Length]
            : new char[trimmedTag.Length];
        int normalizedLength = 0;

        foreach (char character in trimmedTag)
        {
            if (!char.IsWhiteSpace(character))
            {
                normalized[normalizedLength++] = character;
            }
        }

        return normalizedLength == 0
            ? string.Empty
            : new string(normalized[..normalizedLength]);
    }
}
