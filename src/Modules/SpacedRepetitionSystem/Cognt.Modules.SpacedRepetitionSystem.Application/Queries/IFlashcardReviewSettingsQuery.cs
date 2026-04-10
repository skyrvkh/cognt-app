using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Queries;

public interface IFlashcardReviewSettingsQuery
{
    FlashcardReviewSettingsReadModel Execute(FlashcardCollectionQueryRequest request);
}
