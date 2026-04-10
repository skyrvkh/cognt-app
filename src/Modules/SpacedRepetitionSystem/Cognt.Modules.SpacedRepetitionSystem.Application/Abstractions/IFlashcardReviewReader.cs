using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

public interface IFlashcardReviewReader
{
    FlashcardReviewSettingsReadModel LoadReviewSettings(string collectionId);

    FlashcardReviewQueueReadModel LoadReviewQueue(string collectionId);
}
