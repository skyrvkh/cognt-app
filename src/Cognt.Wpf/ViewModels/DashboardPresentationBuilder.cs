using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace CogntApp.ViewModels;

internal sealed record DashboardPresentation(
    IReadOnlyList<DashboardMetricViewModel> Metrics,
    IReadOnlyList<DashboardStudyHeatmapCellViewModel> StudyHeatmapCells,
    IReadOnlyList<DashboardEmptyStateViewModel> EmptyStates);

internal static partial class DashboardPresentationBuilder
{
    public static DashboardPresentation Build(
        ProgressAnalyticsDashboardReadModel dashboard,
        DateOnly today)
    {
        IReadOnlyDictionary<DateOnly, int> reviewedByDate = CreateReviewedByDateMap(dashboard.ReviewActivity);

        return new DashboardPresentation(
            CreateMetrics(dashboard),
            CreateHeatmap(today, reviewedByDate),
            CreateEmptyStates(dashboard));
    }
}
