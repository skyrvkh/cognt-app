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
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => string.Concat(value.Trim().Where(character => !char.IsWhiteSpace(character))))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
    }
}
