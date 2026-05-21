namespace Cognt.Modules.SpacedRepetitionSystem.Application.Commands;

public interface IUpdateFlashcardReviewSettingsCommand
{
    Task ExecuteAsync(
        UpdateFlashcardReviewSettingsRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record UpdateFlashcardReviewSettingsRequest(
    string CollectionId,
    int MaximumReviewCardsPerDay,
    int MaximumNewCardsPerDay,
    double DesiredRetention,
    IReadOnlyList<TimeSpan>? LearningSteps = null,
    IReadOnlyList<TimeSpan>? RelearningSteps = null,
    bool IsOptimizationEnabled = false,
    IReadOnlyList<double>? FsrsParameters = null);
