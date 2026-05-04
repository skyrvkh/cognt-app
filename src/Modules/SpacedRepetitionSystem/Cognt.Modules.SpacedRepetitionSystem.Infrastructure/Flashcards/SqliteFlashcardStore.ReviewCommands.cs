using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;
using AppReadModels = Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using PersistenceRating = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewRatingReadModel;
using PersistenceSettings = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewSettingsReadModel;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    public Task UpdateReviewSettingsAsync(
        string collectionId,
        AppReadModels.FlashcardReviewSettingsReadModel settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        cancellationToken.ThrowIfCancellationRequested();
        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionId(collectionId);
        repository.SaveReviewSettings(ToPersistenceSettings(settings));
        return Task.CompletedTask;
    }

    public async Task RecordReviewAsync(
        string collectionId,
        long cardId,
        AppReadModels.FlashcardReviewRatingReadModel rating,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string collectionPath = FlashcardCollectionStorage.GetExistingCollectionPath(collectionId);

        using SqliteFlashcardRepository repository = FlashcardCollectionStorage.OpenRepositoryForCollectionPath(collectionPath);
        FlashcardRecord card = repository.GetCard(cardId);
        PersistenceSettings settings = repository.LoadReviewSettings();
        FlashcardSchedule schedule = await scheduler.ReviewCardAsync(
            card.SerializedState,
            rating,
            settings.DesiredRetention,
            cancellationToken);

        repository.ApplyReview(cardId, ToPersistenceRating(rating), schedule);
    }

    private static PersistenceSettings ToPersistenceSettings(
        AppReadModels.FlashcardReviewSettingsReadModel settings)
    {
        return new PersistenceSettings(
            settings.MaximumReviewCardsPerDay,
            settings.MaximumNewCardsPerDay,
            settings.DesiredRetention).Normalize();
    }

    private static PersistenceRating ToPersistenceRating(
        AppReadModels.FlashcardReviewRatingReadModel rating)
    {
        return (PersistenceRating)(int)rating;
    }
}
