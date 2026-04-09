namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;

public interface ICollectionProgressAnalyticsDataSource
{
    CollectionProgressAnalyticsSnapshot LoadCollectionSnapshot(
        string collectionId,
        DateOnly today,
        IReadOnlyList<string> selectedDeckNames);
}
