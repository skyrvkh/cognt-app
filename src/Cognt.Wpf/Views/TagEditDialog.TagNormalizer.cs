namespace CogntApp.Views;

internal static class TagEditDialogTagNormalizer
{
    internal static string NormalizeTag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        int whitespaceCount = 0;
        foreach (char character in trimmed)
        {
            if (char.IsWhiteSpace(character))
            {
                whitespaceCount++;
            }
        }

        if (whitespaceCount == 0)
        {
            return trimmed;
        }

        return string.Create(
            trimmed.Length - whitespaceCount,
            trimmed,
            static (destination, source) =>
            {
                int index = 0;
                foreach (char character in source)
                {
                    if (!char.IsWhiteSpace(character))
                    {
                        destination[index++] = character;
                    }
                }
            });
    }

    internal static string[] NormalizeTags(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : NormalizeTags(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    internal static string[] NormalizeTags(IEnumerable<string> values)
    {
        List<string> normalizedTags = [];
        HashSet<string> seenTags = new(StringComparer.OrdinalIgnoreCase);
        foreach (string value in values)
        {
            string tag = NormalizeTag(value);
            if (tag.Length > 0 && seenTags.Add(tag))
            {
                normalizedTags.Add(tag);
            }
        }

        return normalizedTags.ToArray();
    }

    internal static string[] ExceptTags(
        IReadOnlyList<string> source,
        IReadOnlyList<string> excludedTags)
    {
        if (source.Count == 0)
        {
            return [];
        }

        if (excludedTags.Count == 0)
        {
            return source.ToArray();
        }

        HashSet<string> excludedTagSet = new(excludedTags, StringComparer.OrdinalIgnoreCase);
        List<string>? result = null;
        foreach (string tag in source)
        {
            if (!excludedTagSet.Contains(tag))
            {
                result ??= new List<string>(source.Count);
                result.Add(tag);
            }
        }

        return result?.ToArray() ?? [];
    }
}
