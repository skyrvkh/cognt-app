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
        long[] cardIds = EnsureSelectedCardIds(request.CardIds, nameof(request));

        return request with { CardIds = cardIds };
    }

    internal static DeleteFlashcardDeckRequest EnsureDeleteFlashcardDeckRequest(
        DeleteFlashcardDeckRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        long[] deckIds = EnsureSelectedDeckIds(request.DeckIds, nameof(request));

        return request with { DeckIds = deckIds };
    }

    internal static AddFlashcardTagsRequest EnsureAddFlashcardTagsRequest(
        AddFlashcardTagsRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        FlashcardCardTagSelection selection = EnsureCardTagSelection(
            request.CardIds,
            request.Tags,
            nameof(request));

        return request with
        {
            CardIds = selection.CardIds,
            Tags = selection.Tags,
        };
    }

    internal static RemoveFlashcardTagsRequest EnsureRemoveFlashcardTagsRequest(
        RemoveFlashcardTagsRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCollectionId(request.CollectionId);
        FlashcardCardTagSelection selection = EnsureCardTagSelection(
            request.CardIds,
            request.Tags,
            nameof(request));

        return request with
        {
            CardIds = selection.CardIds,
            Tags = selection.Tags,
        };
    }

    internal static RecordFlashcardReviewRequest EnsureRecordReviewRequest(
        RecordFlashcardReviewRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCardCommandTarget(request.CollectionId, request.CardId);
        EnsureReviewRating(request.Rating);
        int? reviewDurationMilliseconds = request.ReviewDurationMilliseconds is > 0
            ? Math.Min(request.ReviewDurationMilliseconds.Value, 3_600_000)
            : null;
        return request with { ReviewDurationMilliseconds = reviewDurationMilliseconds };
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

}
