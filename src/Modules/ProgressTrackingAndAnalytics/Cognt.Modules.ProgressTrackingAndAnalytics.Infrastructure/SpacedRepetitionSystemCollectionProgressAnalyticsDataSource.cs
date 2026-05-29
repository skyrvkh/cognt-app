using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

internal sealed class SpacedRepetitionSystemCollectionProgressAnalyticsDataSource : ICollectionProgressAnalyticsDataSource
{
    private readonly IFlashcardDeckSummariesQuery deckSummariesQuery;
    private readonly IFlashcardDashboardSummaryQuery dashboardSummaryQuery;
    private readonly IFlashcardProgressAnalyticsQuery progressAnalyticsQuery;
    private readonly ISpacedRepetitionSystemProgressAnalyticsSnapshotMapper snapshotMapper;

    public SpacedRepetitionSystemCollectionProgressAnalyticsDataSource(
        IFlashcardDeckSummariesQuery deckSummariesQuery,
        IFlashcardDashboardSummaryQuery dashboardSummaryQuery,
        IFlashcardProgressAnalyticsQuery progressAnalyticsQuery,
        ISpacedRepetitionSystemProgressAnalyticsSnapshotMapper snapshotMapper)
    {
        ArgumentNullException.ThrowIfNull(deckSummariesQuery);
        ArgumentNullException.ThrowIfNull(dashboardSummaryQuery);
        ArgumentNullException.ThrowIfNull(progressAnalyticsQuery);
        ArgumentNullException.ThrowIfNull(snapshotMapper);

        this.deckSummariesQuery = deckSummariesQuery;
        this.dashboardSummaryQuery = dashboardSummaryQuery;
        this.progressAnalyticsQuery = progressAnalyticsQuery;
        this.snapshotMapper = snapshotMapper;
    }

    public CollectionProgressAnalyticsSnapshot LoadCollectionSnapshot(
        string collectionId,
        DateOnly today,
        IReadOnlyList<string> selectedDeckNames)
    {
        FlashcardCollectionQueryRequest collectionQueryRequest = new(collectionId);
        IReadOnlyList<FlashcardDeckSummaryReadModel> allDecks = deckSummariesQuery.Execute(collectionQueryRequest);
        IReadOnlyList<FlashcardDeckSummaryReadModel> decks = SelectedFlashcardDeckFilter.Apply(allDecks, selectedDeckNames);

        FlashcardDashboardSummaryReadModel dashboard = dashboardSummaryQuery.Execute(collectionQueryRequest, selectedDeckNames);

        FlashcardProgressAnalyticsReadModel analytics = progressAnalyticsQuery.Execute(
            new FlashcardProgressAnalyticsQueryRequest(collectionId, today, selectedDeckNames));

        return snapshotMapper.CreateCollectionSnapshot(
            new SpacedRepetitionSystemProgressAnalyticsReadData(decks, dashboard, analytics));
    }
}
