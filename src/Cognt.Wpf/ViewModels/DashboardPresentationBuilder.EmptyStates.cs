using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace CogntApp.ViewModels;

internal static partial class DashboardPresentationBuilder
{
    private static DashboardEmptyStateViewModel[] CreateEmptyStates(ProgressAnalyticsDashboardReadModel dashboard)
    {
        if (dashboard.TotalCards == 0)
        {
            return
            [
                new DashboardEmptyStateViewModel(
                    "No cards yet",
                    "Create cards from your notes to start reviewing this collection."),
            ];
        }

        if (dashboard.ReviewHistoryCount == 0)
        {
            return
            [
                new DashboardEmptyStateViewModel(
                    "Insufficient review data",
                    "Review a few due cards to build activity, recall, and streak data."),
            ];
        }

        return [];
    }
}
