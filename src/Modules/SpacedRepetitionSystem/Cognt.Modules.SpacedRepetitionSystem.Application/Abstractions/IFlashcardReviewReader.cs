using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

public interface IFlashcardReviewReader
{
    FlashcardReviewSettingsReadModel LoadReviewSettings(string collectionId);

    FlashcardReviewQueueReadModel LoadReviewQueue(
        string collectionId,
        FlashcardReviewQueueMode mode = FlashcardReviewQueueMode.Scheduled,
        CramScope? cramScope = null);
}
