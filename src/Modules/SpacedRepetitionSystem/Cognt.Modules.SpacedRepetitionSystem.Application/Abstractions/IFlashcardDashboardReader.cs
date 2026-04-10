using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;

public interface IFlashcardDashboardReader
{
    FlashcardDashboardSummaryReadModel LoadDashboardSummary(
        string collectionId,
        IReadOnlyList<string>? deckNames = null);

    FlashcardProgressAnalyticsReadModel LoadProgressAnalytics(
        string collectionId,
        DateOnly today,
        IReadOnlyList<string>? deckNames = null);
}
