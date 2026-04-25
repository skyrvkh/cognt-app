using Cognt.Modules.SpacedRepetitionSystem.Application;
using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class RecordFlashcardReview(IFlashcardReviewWriter flashcardReviews) : IRecordFlashcardReviewCommand
{
    private readonly IFlashcardReviewWriter reviews = flashcardReviews ?? throw new ArgumentNullException(nameof(flashcardReviews));

    public Task ExecuteAsync(
        RecordFlashcardReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureRecordReviewRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        return reviews.RecordReviewAsync(
            request.CollectionId,
            request.CardId,
            request.Rating,
            cancellationToken);
    }
}
