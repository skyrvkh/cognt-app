using Cognt.Modules.SpacedRepetitionSystem.Application;
using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class RecordFlashcardReview(
    IFlashcardReviewWriter flashcardReviews)
    : IRecordFlashcardReviewCommand
{
    private readonly IFlashcardReviewWriter reviews = flashcardReviews ?? throw new ArgumentNullException(nameof(flashcardReviews));

    public async Task<RecordedFlashcardReviewReadModel> ExecuteAsync(
        RecordFlashcardReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureRecordReviewRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        RecordedFlashcardReviewReadModel result = await reviews.RecordReviewAsync(
            request.CollectionId,
            request.CardId,
            request.Rating,
            request.ReviewDurationMilliseconds,
            cancellationToken).ConfigureAwait(false);
        return result;
    }
}
