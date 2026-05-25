using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

namespace Cognt.Modules.SpacedRepetitionSystem.Application;

internal static partial class FlashcardRequestGuards
{
    internal static CreateFlashcardsRequest EnsureCreateFlashcardsRequest(
        CreateFlashcardsRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DeckName);
        ArgumentNullException.ThrowIfNull(request.Content);
        ArgumentNullException.ThrowIfNull(request.Tags);
        return request;
    }

    internal static UpdateFlashcardReviewSettingsRequest EnsureUpdateReviewSettingsRequest(
        UpdateFlashcardReviewSettingsRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        return request;
    }

    internal static CreateCardsFromCardContextRequest EnsureCreateCardsFromCardContextRequest(
        CreateCardsFromCardContextRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DeckName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceRelPath);
        ArgumentNullException.ThrowIfNull(request.Context);
        ArgumentNullException.ThrowIfNull(request.Tags);

        if (!string.Equals(
                CardContextDraftNoteDocument.NormalizeRelPath(request.SourceRelPath),
                request.Context.SourceRelPath,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("The source path must match the card-context snapshot source path.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Context.Id))
        {
            throw new ArgumentException("The card-context snapshot must have a context id.", nameof(request));
        }

        if (request.Context.Slots.Count == 0 || request.Context.Targets.Count == 0)
        {
            throw new ArgumentException("The card-context snapshot must contain at least one card slot and target.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Context.ContextContent))
        {
            throw new ArgumentException("The card-context snapshot must contain context content.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Context.SnapshotHash))
        {
            throw new ArgumentException("The card-context snapshot must contain a snapshot hash.", nameof(request));
        }

        return request;
    }

    internal static UpdateFlashcardFrontContentRequest EnsureUpdateFrontContentRequest(
        UpdateFlashcardFrontContentRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCardCommandTarget(request.CollectionId, request.CardId);
        ArgumentNullException.ThrowIfNull(request.FrontContent);
        return request;
    }

    internal static UpdateFlashcardContentRequest EnsureUpdateContentRequest(
        UpdateFlashcardContentRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCardCommandTarget(request.CollectionId, request.CardId);
        ArgumentNullException.ThrowIfNull(request.FrontContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DeckName);
        ArgumentNullException.ThrowIfNull(request.Tags);

        return request with
        {
            DeckName = request.DeckName.Trim(),
            Tags = NormalizeStrings(request.Tags),
        };
    }

    internal static DeleteFlashcardsRequest EnsureDeleteFlashcardsRequest(
        DeleteFlashcardsRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        long[] cardIds = NormalizeCardIds(request.CardIds);
        if (cardIds.Length == 0)
        {
            throw new ArgumentException("At least one card must be selected.", nameof(request));
        }

        return request with { CardIds = cardIds };
    }

    internal static AddFlashcardTagsRequest EnsureAddFlashcardTagsRequest(
        AddFlashcardTagsRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        long[] cardIds = NormalizeCardIds(request.CardIds);
        IReadOnlyList<string> tags = NormalizeStrings(request.Tags);
        if (cardIds.Length == 0)
        {
            throw new ArgumentException("At least one card must be selected.", nameof(request));
        }

        if (tags.Count == 0)
        {
            throw new ArgumentException("At least one tag must be supplied.", nameof(request));
        }

        return request with
        {
            CardIds = cardIds,
            Tags = tags,
        };
    }

    internal static RemoveFlashcardTagsRequest EnsureRemoveFlashcardTagsRequest(
        RemoveFlashcardTagsRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        long[] cardIds = NormalizeCardIds(request.CardIds);
        IReadOnlyList<string> tags = NormalizeStrings(request.Tags);
        if (cardIds.Length == 0)
        {
            throw new ArgumentException("At least one card must be selected.", nameof(request));
        }

        if (tags.Count == 0)
        {
            throw new ArgumentException("At least one tag must be supplied.", nameof(request));
        }

        return request with
        {
            CardIds = cardIds,
            Tags = tags,
        };
    }

    internal static RecordFlashcardReviewRequest EnsureRecordReviewRequest(
        RecordFlashcardReviewRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCardCommandTarget(request.CollectionId, request.CardId);
        EnsureReviewRating(request.Rating);
        return request;
    }

    internal static UpdateFlashcardSuspensionRequest EnsureUpdateSuspensionRequest(
        UpdateFlashcardSuspensionRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCardCommandTarget(request.CollectionId, request.CardId);
        return request;
    }

    internal static PostponeFlashcardDueDateRequest EnsurePostponeDueDateRequest(
        PostponeFlashcardDueDateRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCardCommandTarget(request.CollectionId, request.CardId);
        if (request.Days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Days, "Postpone days must be positive.");
        }

        return request;
    }

    private static long[] NormalizeCardIds(IEnumerable<long>? cardIds)
    {
        return cardIds?
            .Where(cardId => cardId > 0)
            .Distinct()
            .ToArray()
            ?? [];
    }
}
