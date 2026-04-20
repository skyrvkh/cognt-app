using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

public interface ICollectionProgressAnalyticsReportQuery
{
    ProgressAnalyticsReport Execute(CollectionProgressAnalyticsReportQueryRequest request);
}

public sealed record CollectionProgressAnalyticsReportQueryRequest
{
    public CollectionProgressAnalyticsReportQueryRequest(
        string collectionId,
        DateOnly today,
        IReadOnlyList<string>? selectedDeckNames = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);

        CollectionId = collectionId;
        Today = today;
        SelectedDeckNames = selectedDeckNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    public string CollectionId { get; }

    public DateOnly Today { get; }

    public IReadOnlyList<string> SelectedDeckNames { get; }
}
