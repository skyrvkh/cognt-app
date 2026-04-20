using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

public interface ICollectionProgressAnalyticsDashboardQuery
{
    ProgressAnalyticsDashboardReadModel Execute(CollectionProgressAnalyticsDashboardQueryRequest request);
}

public sealed record CollectionProgressAnalyticsDashboardQueryRequest
{
    public CollectionProgressAnalyticsDashboardQueryRequest(string collectionId, DateOnly today)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);

        CollectionId = collectionId;
        Today = today;
    }

    public string CollectionId { get; }

    public DateOnly Today { get; }
}
