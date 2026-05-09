using System.IO;
using System.Windows.Input;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;
using CogntApp.ViewModels.Decks.PresentationModels;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace CogntApp.ViewModels.Decks;

public sealed partial class StatisticsSubviewViewModel : ObservableObject
{
    private readonly ICollectionProgressAnalyticsReportQuery collectionProgressAnalyticsReportQuery;

    private IReadOnlyList<DeckPerformanceRow> deckPerformanceRows = [];
    private string cardsReviewedValue = "0";
    private string cardsReviewedDetail = "Previous 14 days";
    private string studyStreakValue = "0 days";
    private string studyStreakDetail = "Best: 0 days";
    private string averageRecallValue = "0%";
    private string averageRecallDetail = "Based on reviewed cards";
    private string dueTomorrowValue = "0";
    private string dueTomorrowDetail = "Across 0 decks";
    private string scopeLabel = "All decks";
    private ICommand backToDecksCommand = new RelayCommand(() => { });
    private ISeries[] dailyStatisticsSeries = [];
    private Axis[] dailyStatisticsXAxes = [];
    private Axis[] dailyStatisticsYAxes = [];
    private ISeries[] recallDistributionSeries = [];
    private ISeries[] studyStreakSeries = [];
    private Axis[] studyStreakXAxes = [];
    private Axis[] studyStreakYAxes = [];
    private ISeries[] weeklyOutcomeSeries = [];
    private Axis[] weeklyOutcomeXAxes = [];
    private Axis[] weeklyOutcomeYAxes = [];

    public StatisticsSubviewViewModel(ICollectionProgressAnalyticsReportQuery collectionProgressAnalyticsReportQuery)
    {
        ArgumentNullException.ThrowIfNull(collectionProgressAnalyticsReportQuery);

        this.collectionProgressAnalyticsReportQuery = collectionProgressAnalyticsReportQuery;
        LoadEmptyState();
    }

    public IReadOnlyList<DeckPerformanceRow> DeckPerformanceRows
    {
        get => deckPerformanceRows;
        private set => SetProperty(ref deckPerformanceRows, value);
    }

    public string CardsReviewedValue
    {
        get => cardsReviewedValue;
        private set => SetProperty(ref cardsReviewedValue, value);
    }

    public string CardsReviewedDetail
    {
        get => cardsReviewedDetail;
        private set => SetProperty(ref cardsReviewedDetail, value);
    }

    public string StudyStreakValue
    {
        get => studyStreakValue;
        private set => SetProperty(ref studyStreakValue, value);
    }

    public string StudyStreakDetail
    {
        get => studyStreakDetail;
        private set => SetProperty(ref studyStreakDetail, value);
    }

    public string AverageRecallValue
    {
        get => averageRecallValue;
        private set => SetProperty(ref averageRecallValue, value);
    }

    public string AverageRecallDetail
    {
        get => averageRecallDetail;
        private set => SetProperty(ref averageRecallDetail, value);
    }

    public string DueTomorrowValue
    {
        get => dueTomorrowValue;
        private set => SetProperty(ref dueTomorrowValue, value);
    }

    public string DueTomorrowDetail
    {
        get => dueTomorrowDetail;
        private set => SetProperty(ref dueTomorrowDetail, value);
    }

    public string ScopeLabel
    {
        get => scopeLabel;
        private set => SetProperty(ref scopeLabel, value);
    }

    public ICommand BackToDecksCommand
    {
        get => backToDecksCommand;
        private set => SetProperty(ref backToDecksCommand, value);
    }

    public ISeries[] DailyStatisticsSeries
    {
        get => dailyStatisticsSeries;
        private set => SetProperty(ref dailyStatisticsSeries, value);
    }

    public Axis[] DailyStatisticsXAxes
    {
        get => dailyStatisticsXAxes;
        private set => SetProperty(ref dailyStatisticsXAxes, value);
    }

    public Axis[] DailyStatisticsYAxes
    {
        get => dailyStatisticsYAxes;
        private set => SetProperty(ref dailyStatisticsYAxes, value);
    }

    public ISeries[] RecallDistributionSeries
    {
        get => recallDistributionSeries;
        private set => SetProperty(ref recallDistributionSeries, value);
    }

    public ISeries[] StudyStreakSeries
    {
        get => studyStreakSeries;
        private set => SetProperty(ref studyStreakSeries, value);
    }

    public Axis[] StudyStreakXAxes
    {
        get => studyStreakXAxes;
        private set => SetProperty(ref studyStreakXAxes, value);
    }

    public Axis[] StudyStreakYAxes
    {
        get => studyStreakYAxes;
        private set => SetProperty(ref studyStreakYAxes, value);
    }

    public ISeries[] WeeklyOutcomeSeries
    {
        get => weeklyOutcomeSeries;
        private set => SetProperty(ref weeklyOutcomeSeries, value);
    }

    public Axis[] WeeklyOutcomeXAxes
    {
        get => weeklyOutcomeXAxes;
        private set => SetProperty(ref weeklyOutcomeXAxes, value);
    }

    public Axis[] WeeklyOutcomeYAxes
    {
        get => weeklyOutcomeYAxes;
        private set => SetProperty(ref weeklyOutcomeYAxes, value);
    }

    public void LoadCollection(
        string? collectionId,
        IReadOnlyList<string>? deckNames = null)
    {
        string[] selectedDeckNames = NormalizeDeckNames(deckNames);
        ScopeLabel = FormatScopeLabel(selectedDeckNames);

        if (string.IsNullOrWhiteSpace(collectionId) || !Directory.Exists(collectionId))
        {
            LoadEmptyState();
            return;
        }

        string existingCollectionId = collectionId;
        ProgressAnalyticsReport report = collectionProgressAnalyticsReportQuery.Execute(
            new CollectionProgressAnalyticsReportQueryRequest(
                existingCollectionId,
                DateOnly.FromDateTime(DateTime.Today),
                selectedDeckNames));

        LoadReport(report);
    }

    internal void SetBackToDecksAction(Action backToDecks)
    {
        ArgumentNullException.ThrowIfNull(backToDecks);

        BackToDecksCommand = new RelayCommand(backToDecks);
    }

    private static string[] NormalizeDeckNames(IReadOnlyList<string>? deckNames)
    {
        return deckNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    private static string FormatScopeLabel(IReadOnlyList<string> deckNames)
    {
        return deckNames.Count switch
        {
            0 => "All decks",
            1 => string.Concat("Deck: ", deckNames[0]),
            _ => string.Concat(deckNames.Count.ToString("N0"), " selected decks")
        };
    }
}
