using Cognt.Modules.SpacedRepetitionSystem.Application;
using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

internal sealed class GetFlashcardReviewSettings(IFlashcardReviewReader flashcardReviews) : IFlashcardReviewSettingsQuery
{
    private readonly IFlashcardReviewReader reviews = flashcardReviews ?? throw new ArgumentNullException(nameof(flashcardReviews));

    public FlashcardReviewSettingsReadModel Execute(FlashcardCollectionQueryRequest request)
    {
        request = FlashcardRequestGuards.EnsureCollectionQueryRequest(request);

        return reviews.LoadReviewSettings(request.CollectionId);
    }
}
