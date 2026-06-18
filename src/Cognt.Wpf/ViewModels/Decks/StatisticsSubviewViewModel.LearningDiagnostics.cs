using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace CogntApp.ViewModels.Decks;

public sealed partial class StatisticsSubviewViewModel
{
    private string forecast7Value = "0";
    private string forecast7Detail = string.Empty;
    private string forecast30Value = "0";
    private string forecast30Detail = string.Empty;
    private string forecast90Value = "0";
    private string forecast90Detail = string.Empty;
    private string retentionCalibrationValue = "—";
    private string retentionCalibrationDetail = string.Empty;
    private string reviewLatencyValue = "—";
    private string reviewLatencyDetail = string.Empty;
    private string lapseReportValue = "0";
    private string lapseReportDetail = string.Empty;
    private string smoothingValue = "0/day";
    private string smoothingDetail = string.Empty;
    private string tomorrowWorkloadDetail = string.Empty;
    private string stabilityDistributionLabel = string.Empty;
    private string difficultyDistributionLabel = string.Empty;
    private ISeries[] workloadForecastSeries = [];
    private Axis[] workloadForecastXAxes = [];
    private Axis[] workloadForecastYAxes = [];

    public string Forecast7Value
    {
        get => forecast7Value;
        private set => SetProperty(ref forecast7Value, value);
    }

    public string Forecast7Detail
    {
        get => forecast7Detail;
        private set => SetProperty(ref forecast7Detail, value);
    }

    public string Forecast30Value
    {
        get => forecast30Value;
        private set => SetProperty(ref forecast30Value, value);
    }

    public string Forecast30Detail
    {
        get => forecast30Detail;
        private set => SetProperty(ref forecast30Detail, value);
    }

    public string Forecast90Value
    {
        get => forecast90Value;
        private set => SetProperty(ref forecast90Value, value);
    }

    public string Forecast90Detail
    {
        get => forecast90Detail;
        private set => SetProperty(ref forecast90Detail, value);
    }

    public string RetentionCalibrationValue
    {
        get => retentionCalibrationValue;
        private set => SetProperty(ref retentionCalibrationValue, value);
    }

    public string RetentionCalibrationDetail
    {
        get => retentionCalibrationDetail;
        private set => SetProperty(ref retentionCalibrationDetail, value);
    }

    public string ReviewLatencyValue
    {
        get => reviewLatencyValue;
        private set => SetProperty(ref reviewLatencyValue, value);
    }

    public string ReviewLatencyDetail
    {
        get => reviewLatencyDetail;
        private set => SetProperty(ref reviewLatencyDetail, value);
    }

    public string LapseReportValue
    {
        get => lapseReportValue;
        private set => SetProperty(ref lapseReportValue, value);
    }

    public string LapseReportDetail
    {
        get => lapseReportDetail;
        private set => SetProperty(ref lapseReportDetail, value);
    }

    public string SmoothingValue
    {
        get => smoothingValue;
        private set => SetProperty(ref smoothingValue, value);
    }

    public string SmoothingDetail
    {
        get => smoothingDetail;
        private set => SetProperty(ref smoothingDetail, value);
    }

    public string TomorrowWorkloadDetail
    {
        get => tomorrowWorkloadDetail;
        private set => SetProperty(ref tomorrowWorkloadDetail, value);
    }

    public string StabilityDistributionLabel
    {
        get => stabilityDistributionLabel;
        private set => SetProperty(ref stabilityDistributionLabel, value);
    }

    public string DifficultyDistributionLabel
    {
        get => difficultyDistributionLabel;
        private set => SetProperty(ref difficultyDistributionLabel, value);
    }

    public ISeries[] WorkloadForecastSeries
    {
        get => workloadForecastSeries;
        private set => SetProperty(ref workloadForecastSeries, value);
    }

    public Axis[] WorkloadForecastXAxes
    {
        get => workloadForecastXAxes;
        private set => SetProperty(ref workloadForecastXAxes, value);
    }

    public Axis[] WorkloadForecastYAxes
    {
        get => workloadForecastYAxes;
        private set => SetProperty(ref workloadForecastYAxes, value);
    }
}
