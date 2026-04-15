using Cognt.Modules.SpacedRepetitionSystem.Application;
using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class UpdateFlashcardReviewSettings(IFlashcardReviewWriter flashcardReviews) : IUpdateFlashcardReviewSettingsCommand
{
    private readonly IFlashcardReviewWriter reviews = flashcardReviews ?? throw new ArgumentNullException(nameof(flashcardReviews));

    public Task ExecuteAsync(
        UpdateFlashcardReviewSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureUpdateReviewSettingsRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        return reviews.UpdateReviewSettingsAsync(
            request.CollectionId,
            new FlashcardReviewSettingsReadModel(
                request.MaximumReviewCardsPerDay,
                request.MaximumNewCardsPerDay,
                request.DesiredRetention).Normalize(),
            cancellationToken);
    }
}
