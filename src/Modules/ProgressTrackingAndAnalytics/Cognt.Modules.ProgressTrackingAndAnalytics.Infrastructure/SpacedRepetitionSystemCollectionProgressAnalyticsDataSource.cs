using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal sealed class SpacedRepetitionSystemCollectionProgressAnalyticsDataSource : ICollectionProgressAnalyticsDataSource
{
    private readonly IFlashcardDeckSummariesQuery deckSummariesQuery;
    private readonly IFlashcardDashboardSummaryQuery dashboardSummaryQuery;
    private readonly IFlashcardProgressAnalyticsQuery progressAnalyticsQuery;

    public SpacedRepetitionSystemCollectionProgressAnalyticsDataSource(
        IFlashcardDeckSummariesQuery deckSummariesQuery,
        IFlashcardDashboardSummaryQuery dashboardSummaryQuery,
        IFlashcardProgressAnalyticsQuery progressAnalyticsQuery)
    {
        ArgumentNullException.ThrowIfNull(deckSummariesQuery);
        ArgumentNullException.ThrowIfNull(dashboardSummaryQuery);
        ArgumentNullException.ThrowIfNull(progressAnalyticsQuery);

        this.deckSummariesQuery = deckSummariesQuery;
        this.dashboardSummaryQuery = dashboardSummaryQuery;
        this.progressAnalyticsQuery = progressAnalyticsQuery;
    }

    public CollectionProgressAnalyticsSnapshot LoadCollectionSnapshot(
        string collectionId,
        DateOnly today,
        IReadOnlyList<string> selectedDeckNames)
    {
        FlashcardCollectionQueryRequest collectionQueryRequest = new(collectionId);
        HashSet<string> selectedDeckNameSet = selectedDeckNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks = deckSummariesQuery
            .Execute(collectionQueryRequest)
            .Where(deck => selectedDeckNameSet.Count == 0 || selectedDeckNameSet.Contains(deck.Name))
            .ToArray();
        FlashcardDashboardSummaryReadModel dashboard = dashboardSummaryQuery.Execute(collectionQueryRequest, selectedDeckNames);
        FlashcardProgressAnalyticsReadModel analytics = progressAnalyticsQuery.Execute(
            new FlashcardProgressAnalyticsQueryRequest(collectionId, today, selectedDeckNames));

        return SpacedRepetitionSystemCollectionProgressAnalyticsSnapshotFactory.CreateCollectionSnapshot(decks, dashboard, analytics);
    }
}
