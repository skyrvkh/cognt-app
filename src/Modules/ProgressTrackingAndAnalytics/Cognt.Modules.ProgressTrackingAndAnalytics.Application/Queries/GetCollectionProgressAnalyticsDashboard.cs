using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

public sealed class GetCollectionProgressAnalyticsDashboard : ICollectionProgressAnalyticsDashboardQuery
{
    private readonly ICollectionProgressAnalyticsDataSource collectionAnalyticsDataSource;

    public GetCollectionProgressAnalyticsDashboard(ICollectionProgressAnalyticsDataSource collectionAnalyticsDataSource)
    {
        this.collectionAnalyticsDataSource = collectionAnalyticsDataSource ?? throw new ArgumentNullException(nameof(collectionAnalyticsDataSource));
    }

    public ProgressAnalyticsDashboardReadModel Execute(CollectionProgressAnalyticsDashboardQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        CollectionProgressAnalyticsSnapshot collectionSnapshot = collectionAnalyticsDataSource.LoadCollectionSnapshot(
            request.CollectionId,
            request.Today,
            []);
        return ProgressAnalyticsDashboardFactory.Create(collectionSnapshot.Dashboard, request.Today);
    }
}
