using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private (int NoteCount, int CardCount) GetCollectionCounts(string collectionPath)
    {
        return CollectionCountReader.Read(
            collectionPath,
            path => LoadNoteCatalog(path).Count,
            path => collectionProgressAnalyticsDashboardQuery.Execute(
                new CollectionProgressAnalyticsDashboardQueryRequest(
                    path,
                    DateOnly.FromDateTime(DateTime.Today)))
                .TotalCards,
            logger);
    }
}
