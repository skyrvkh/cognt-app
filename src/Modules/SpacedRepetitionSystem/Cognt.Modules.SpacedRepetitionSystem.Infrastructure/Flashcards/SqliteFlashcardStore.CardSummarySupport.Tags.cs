using System.Text;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal static partial class CardSummaryMaterializer
{
    private static string[] NormalizeTagFilters(string? tag, IReadOnlyList<string>? tags)
    {
        IEnumerable<string?> source = tags is null ? [tag] : tags.Cast<string?>().Append(tag);
        return source
            .Select(NormalizeTagFilter)
            .Where(static normalizedTag => normalizedTag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeTagFilter(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> trimmedTag = tag.AsSpan().Trim();
        int firstWhitespaceIndex = IndexOfWhitespace(trimmedTag);
        if (firstWhitespaceIndex < 0)
        {
            return trimmedTag.Length == tag.Length ? tag : trimmedTag.ToString();
        }

        StringBuilder builder = new(trimmedTag.Length);
        builder.Append(trimmedTag[..firstWhitespaceIndex]);
        for (int index = firstWhitespaceIndex + 1; index < trimmedTag.Length; index++)
        {
            char character = trimmedTag[index];
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static int IndexOfWhitespace(ReadOnlySpan<char> value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool MatchesAnyTag(IReadOnlyList<string> tags, IReadOnlyList<string> normalizedTags)
    {
        return normalizedTags.Count == 0
            || tags.Select(NormalizeTagFilter).Any(tag => ContainsTag(normalizedTags, tag));
    }

    private static bool ContainsTag(IReadOnlyList<string> tags, string candidate)
    {
        return candidate.Length > 0 && tags.Contains(candidate, StringComparer.OrdinalIgnoreCase);
    }
}
