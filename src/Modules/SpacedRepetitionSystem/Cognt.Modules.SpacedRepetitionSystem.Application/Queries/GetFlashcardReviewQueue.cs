using Cognt.Modules.SpacedRepetitionSystem.Application;
using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

internal sealed class GetFlashcardReviewQueue(IFlashcardReviewReader flashcardReviews) : IFlashcardReviewQueueQuery
{
    private readonly IFlashcardReviewReader reviews = flashcardReviews ?? throw new ArgumentNullException(nameof(flashcardReviews));

    public FlashcardReviewQueueReadModel Execute(FlashcardCollectionQueryRequest request)
    {
        request = FlashcardRequestGuards.EnsureCollectionQueryRequest(request);

        return reviews.LoadReviewQueue(request.CollectionId);
    }
}
