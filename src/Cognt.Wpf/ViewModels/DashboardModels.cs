namespace CogntApp.ViewModels;

public sealed record DashboardMetricViewModel(
    string Label,
    string Value,
    string Detail,
    string AccentDetail);

public sealed record DashboardEmptyStateViewModel(
    string Title,
    string Message);

public sealed record DashboardStudyHeatmapCellViewModel(
    DateOnly Date,
    int CardsReviewed,
    string FillColor)
{
    public string Tooltip => $"{CardsReviewed:N0} cards studied on {Date:MMM d, yyyy}";
}
