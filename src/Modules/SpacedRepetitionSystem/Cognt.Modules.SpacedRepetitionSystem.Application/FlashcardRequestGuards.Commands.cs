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

    internal static RecordFlashcardReviewRequest EnsureRecordReviewRequest(
        RecordFlashcardReviewRequest? request)
    {
        request = EnsureRequest(request);
        EnsureCardCommandTarget(request.CollectionId, request.CardId);
        EnsureReviewRating(request.Rating);
        return request;
    }
}
