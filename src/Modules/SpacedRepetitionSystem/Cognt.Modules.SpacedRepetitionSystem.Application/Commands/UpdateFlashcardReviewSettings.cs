using Cognt.Modules.SpacedRepetitionSystem.Application;
using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

internal sealed class UpdateFlashcardReviewSettings(
    IFlashcardReviewWriter flashcardReviews)
    : IUpdateFlashcardReviewSettingsCommand
{
    private readonly IFlashcardReviewWriter reviews = flashcardReviews ?? throw new ArgumentNullException(nameof(flashcardReviews));

    public async Task ExecuteAsync(
        UpdateFlashcardReviewSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        request = FlashcardRequestGuards.EnsureUpdateReviewSettingsRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        FlashcardReviewSettingsReadModel settings = new(
            request.MaximumReviewCardsPerDay,
            request.MaximumNewCardsPerDay,
            request.DesiredRetention,
            request.LearningSteps,
            request.RelearningSteps,
            request.IsOptimizationEnabled,
            request.FsrsParameters,
            request.IsCramEnabled,
            request.ShowFlashcardsInNavigationTree);
        FlashcardReviewSettingsReadModel normalizedSettings = settings.Normalize();
        await reviews.UpdateReviewSettingsAsync(
            request.CollectionId,
            normalizedSettings,
            cancellationToken).ConfigureAwait(false);
    }
}
