using System.Text;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

namespace Cognt.Modules.SpacedRepetitionSystem.Application;

internal static partial class FlashcardRequestGuards
{
    internal static FlashcardCollectionQueryRequest EnsureCollectionQueryRequest(
        FlashcardCollectionQueryRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        return request;
    }

    internal static FlashcardReviewQueueQueryRequest EnsureReviewQueueQueryRequest(
        FlashcardReviewQueueQueryRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        return request with
        {
            CramScope = request.CramScope?.Normalize(),
        };
    }

    internal static FlashcardCardsQueryRequest EnsureCardsQueryRequest(
        FlashcardCardsQueryRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        return request with
        {
            SearchText = string.IsNullOrWhiteSpace(request.SearchText)
                ? null
                : request.SearchText.Trim(),
            Tags = NormalizeStrings(request.Tags),
        };
    }

    internal static FlashcardProgressAnalyticsQueryRequest EnsureProgressAnalyticsQueryRequest(
        FlashcardProgressAnalyticsQueryRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        return request;
    }

    internal static NoteToCardQueueQueryRequest EnsureNoteToCardQueueQueryRequest(
        NoteToCardQueueQueryRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceRelPath);
        ArgumentNullException.ThrowIfNull(request.NoteContent);
        ArgumentNullException.ThrowIfNull(request.DraftDocument);

        if (!string.Equals(
                CardContextDraftNoteDocument.NormalizeRelPath(request.SourceRelPath),
                request.DraftDocument.SourceRelPath,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("The source path must match the card-context draft document source path.", nameof(request));
        }

        return request;
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
            return trimmedValue.ToString();
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
}
