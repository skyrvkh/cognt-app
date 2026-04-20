using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

public sealed class GetCollectionProgressAnalyticsReport : ICollectionProgressAnalyticsReportQuery
{
    private readonly ICollectionProgressAnalyticsDataSource collectionAnalyticsDataSource;

    public GetCollectionProgressAnalyticsReport(ICollectionProgressAnalyticsDataSource collectionAnalyticsDataSource)
    {
        ArgumentNullException.ThrowIfNull(collectionAnalyticsDataSource);

        this.collectionAnalyticsDataSource = collectionAnalyticsDataSource;
    }

    public ProgressAnalyticsReport Execute(CollectionProgressAnalyticsReportQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        CollectionProgressAnalyticsSnapshot collectionSnapshot = collectionAnalyticsDataSource.LoadCollectionSnapshot(
            request.CollectionId,
            request.Today,
            request.SelectedDeckNames);
        return ProgressAnalyticsReportFactory.Create(collectionSnapshot, request.Today);
    }
}
