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

}
