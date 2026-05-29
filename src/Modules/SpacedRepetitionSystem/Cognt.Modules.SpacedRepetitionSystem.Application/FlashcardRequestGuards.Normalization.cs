using System.Text;

namespace Cognt.Modules.SpacedRepetitionSystem.Application;

internal static partial class FlashcardRequestGuards
{
    private static long[] EnsureSelectedCardIds(IEnumerable<long>? cardIds, string paramName)
    {
        return EnsureSelectedIds(cardIds, paramName, "At least one card must be selected.");
    }

    private static long[] EnsureSelectedDeckIds(IEnumerable<long>? deckIds, string paramName)
    {
        return EnsureSelectedIds(deckIds, paramName, "At least one deck must be selected.");
    }

    private static IReadOnlyList<string> EnsureSuppliedTags(IEnumerable<string>? tags, string paramName)
    {
        IReadOnlyList<string> normalizedTags = NormalizeStrings(tags);
        if (normalizedTags.Count == 0)
        {
            throw new ArgumentException("At least one tag must be supplied.", paramName);
        }

        return normalizedTags;
    }

    private static FlashcardCardTagSelection EnsureCardTagSelection(
        IEnumerable<long>? cardIds,
        IEnumerable<string>? tags,
        string paramName)
    {
        return new FlashcardCardTagSelection(
            EnsureSelectedCardIds(cardIds, paramName),
            EnsureSuppliedTags(tags, paramName));
    }

    private static long[] NormalizeCardIds(IEnumerable<long>? cardIds)
    {
        return NormalizeIds(cardIds);
    }

    private static long[] NormalizeDeckIds(IEnumerable<long>? deckIds)
    {
        return NormalizeIds(deckIds);
    }

    private static long[] EnsureSelectedIds(
        IEnumerable<long>? ids,
        string paramName,
        string message)
    {
        long[] normalizedIds = NormalizeIds(ids);
        return normalizedIds.Length == 0
            ? throw new ArgumentException(message, paramName)
            : normalizedIds;
    }

    private static long[] NormalizeIds(IEnumerable<long>? ids)
    {
        return ids?
            .Where(id => id > 0)
            .Distinct()
            .ToArray()
            ?? [];
    }

    private static IReadOnlyList<string> NormalizeStrings(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        HashSet<string> seenValues = new(StringComparer.OrdinalIgnoreCase);
        List<string> normalizedValues = [];
        foreach (string value in values)
        {
            string normalizedValue = NormalizeFilterValue(value);
            if (normalizedValue.Length > 0 && seenValues.Add(normalizedValue))
            {
                normalizedValues.Add(normalizedValue);
            }
        }

        normalizedValues.Sort(StringComparer.OrdinalIgnoreCase);
        return normalizedValues.ToArray();
    }

    private static string NormalizeFilterValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> trimmedValue = value.AsSpan().Trim();
        int firstWhitespaceIndex = IndexOfWhitespace(trimmedValue);
        if (firstWhitespaceIndex < 0)
        {
            return trimmedValue.Length == value.Length ? value : trimmedValue.ToString();
        }

        StringBuilder builder = new(trimmedValue.Length);
        builder.Append(trimmedValue[..firstWhitespaceIndex]);
        for (int index = firstWhitespaceIndex + 1; index < trimmedValue.Length; index++)
        {
            char character = trimmedValue[index];
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

    private sealed record FlashcardCardTagSelection(long[] CardIds, IReadOnlyList<string> Tags);
}
