using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

public interface IFlashcardReviewQueueQuery
{
    FlashcardReviewQueueReadModel Execute(FlashcardCollectionQueryRequest request);

    FlashcardReviewQueueReadModel Execute(FlashcardReviewQueueQueryRequest request);
}

public sealed record FlashcardReviewQueueQueryRequest(
    string CollectionId,
    FlashcardReviewQueueMode Mode = FlashcardReviewQueueMode.Scheduled,
    CramScope? CramScope = null);
