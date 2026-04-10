using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

public interface IFlashcardReviewQueueQuery
{
    FlashcardReviewQueueReadModel Execute(FlashcardCollectionQueryRequest request);
}
