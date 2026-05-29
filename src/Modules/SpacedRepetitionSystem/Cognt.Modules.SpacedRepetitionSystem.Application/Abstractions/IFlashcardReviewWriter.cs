using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

public interface IFlashcardReviewWriter
{
    Task UpdateReviewSettingsAsync(
        string collectionId,
        FlashcardReviewSettingsReadModel settings,
        CancellationToken cancellationToken = default);

    Task<RecordedFlashcardReviewReadModel> RecordReviewAsync(
        string collectionId,
        long cardId,
        FlashcardReviewRatingReadModel rating,
        CancellationToken cancellationToken = default);

    Task SetCardSuspensionAsync(
        string collectionId,
        long cardId,
        bool isSuspended,
        CancellationToken cancellationToken = default);

    Task PostponeCardDueDateAsync(
        string collectionId,
        long cardId,
        int days,
        CancellationToken cancellationToken = default);
}
