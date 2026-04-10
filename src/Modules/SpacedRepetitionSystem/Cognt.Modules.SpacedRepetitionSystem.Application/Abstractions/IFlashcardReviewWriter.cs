using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

public interface IFlashcardReviewWriter
{
    Task UpdateReviewSettingsAsync(
        string collectionId,
        FlashcardReviewSettingsReadModel settings,
        CancellationToken cancellationToken = default);

    Task RecordReviewAsync(
        string collectionId,
        long cardId,
        FlashcardReviewRatingReadModel rating,
        CancellationToken cancellationToken = default);
}
