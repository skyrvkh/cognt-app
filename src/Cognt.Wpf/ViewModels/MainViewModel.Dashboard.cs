using System.IO;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;
using Microsoft.Extensions.Logging;
using Navigation = CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void ShowDashboard()
    {
        SaveCurrentNoteFromEditor();
        RefreshDashboardData();
        ClearCurrentNoteState();
        OverlayRequested?.Invoke(this, new Navigation.OverlayRequestedEventArgs(Navigation.OverlayViewKind.Dashboard));
    }

    private void RefreshDashboardData()
    {
        DashboardMetrics.Clear();
        DashboardStudyHeatmapCells.Clear();
        DashboardEmptyStates.Clear();

        ProgressAnalyticsDashboardReadModel dashboard = new(0, 0, 0, 0, 0, 0, 0, 0, []);

        if (!string.IsNullOrWhiteSpace(openCollectionPath) && Directory.Exists(openCollectionPath))
        {
            try
            {
                dashboard = collectionProgressAnalyticsDashboardQuery.Execute(
                    new CollectionProgressAnalyticsDashboardQueryRequest(
                        openCollectionPath,
                        DateOnly.FromDateTime(DateTime.Today)));
            }
            catch (Exception ex) when (IsRecoverableFileSystemException(ex))
            {
                logger?.LogError(ex, "Dashboard data refresh failed with recoverable file-system exception. CollectionPath={CollectionPath}", openCollectionPath);
                dashboard = new ProgressAnalyticsDashboardReadModel(0, 0, 0, 0, 0, 0, 0, 0, []);
            }
        }
        DashboardPresentation presentation = DashboardPresentationBuilder.Build(
            dashboard,
            DateOnly.FromDateTime(DateTime.Today));
        foreach (DashboardEmptyStateViewModel emptyState in presentation.EmptyStates)
        {
            DashboardEmptyStates.Add(emptyState);
        }

        foreach (DashboardMetricViewModel metric in presentation.Metrics)
        {
            DashboardMetrics.Add(metric);
        }

        foreach (DashboardStudyHeatmapCellViewModel cell in presentation.StudyHeatmapCells)
        {
            DashboardStudyHeatmapCells.Add(cell);
        }
    }
}
