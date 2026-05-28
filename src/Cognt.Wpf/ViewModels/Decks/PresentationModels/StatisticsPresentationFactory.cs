using System.Globalization;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;
using CogntApp.Design;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;

namespace CogntApp.ViewModels.Decks.PresentationModels;

internal static partial class StatisticsPresentationFactory
{
    private const int HeatmapWeekCount = 52;
    private const int DaysPerWeek = 7;
    private const int MovingAverageWindowDays = 7;

    internal static StatisticsPresentationState Create(ProgressAnalyticsReport report, DateOnly today)
    {
        IReadOnlyDictionary<DateOnly, int> reviewedByDate = CreateReviewedByDateMap(report.StudyStreak);
        DailyStudyChartData dailyChartData = CreateDailyStudyChartData(report.DailyStudyActivity);
        WeeklyReviewChartData weeklyChartData = CreateWeeklyReviewChartData(report.WeeklyReviewOutcomes);

        return new StatisticsPresentationState(
            report.Summary.CardsReviewed.ToString("N0", CultureInfo.InvariantCulture),
            "Persisted review history",
            FormatDays(report.Summary.CurrentStreakDays),
            $"Best: {FormatDays(report.Summary.BestStreakDays)}",
            string.Concat(
                report.Summary.AverageRecallPercent.ToString(CultureInfo.InvariantCulture),
                "%"),
            "Good/Easy review share",
            report.Summary.DueTomorrowCards.ToString("N0", CultureInfo.InvariantCulture),
            $"Across {FormatDeckCount(report.Summary.DueTomorrowDecks)}",
            CreateDeckPerformanceRows(report.DeckPerformance),
            [
                CreateColumnSeries("Cards Reviewed", dailyChartData.CardsReviewed, ThemeResourceLookup.ColorHex("AccentBrush", "#2563EB"), 12),
                CreateMovingAverageSeries("Cards Reviewed 7-day avg", dailyChartData.CardsReviewed, ThemeResourceLookup.ColorHex("GreenBrush", "#0F766E"))
            ],
            [CreateCategoryAxis(dailyChartData.Labels)],
            [CreateValueAxis()],
            CreateRecallPieSeries(report.RecallDistribution),
            [
                new HeatSeries<WeightedPoint>
                {
                    Name = "Cards",
                    Values = CreateStudyHeatmapPoints(today, reviewedByDate),
                    HeatMap =
                    [
                        SKColor.Parse(ThemeResourceLookup.ColorHex("PanelAltBrush", "#F8FAFC")).AsLvcColor(),
                        SKColor.Parse(ThemeResourceLookup.ColorHex("HeatmapLowBrush", "#DDE5FF")).AsLvcColor(),
                        SKColor.Parse(ThemeResourceLookup.ColorHex("HeatmapMediumBrush", "#AEBEFF")).AsLvcColor(),
                        SKColor.Parse(ThemeResourceLookup.ColorHex("HeatmapHighBrush", "#7E92FF")).AsLvcColor(),
                        SKColor.Parse(ThemeResourceLookup.ColorHex("AccentBrush", "#5867FF")).AsLvcColor()
                    ]
                }
            ],
            [CreateHeatmapXAxis(today)],
            [CreateHeatmapYAxis()],
            [
                CreateStackedColumnSeries("Again", weeklyChartData.Again, ThemeResourceLookup.ColorHex("RedBrush", "#DC2626")),
                CreateStackedColumnSeries("Hard", weeklyChartData.Hard, ThemeResourceLookup.ColorHex("OrangeBrush", "#D97706")),
                CreateStackedColumnSeries("Good", weeklyChartData.Good, ThemeResourceLookup.ColorHex("BlueBrush", "#2563EB")),
                CreateStackedColumnSeries("Easy", weeklyChartData.Easy, ThemeResourceLookup.ColorHex("GreenBrush", "#16A34A"))
            ],
            [CreateCategoryAxis(weeklyChartData.Labels)],
            [CreateValueAxis()]);
    }

    private static IReadOnlyDictionary<DateOnly, int> CreateReviewedByDateMap(
        IReadOnlyList<ProgressAnalyticsActivityDayReadModel> studyStreak)
    {
        Dictionary<DateOnly, int> reviewedByDate = new(studyStreak.Count);
        for (int index = 0; index < studyStreak.Count; index++)
        {
            ProgressAnalyticsActivityDayReadModel day = studyStreak[index];
            reviewedByDate.Add(day.Date, day.CardsReviewed);
        }

        return reviewedByDate;
    }

    private static DeckPerformanceRow[] CreateDeckPerformanceRows(
        IReadOnlyList<DeckPerformanceReadModel> deckPerformance)
    {
        DeckPerformanceRow[] rows = new DeckPerformanceRow[deckPerformance.Count];
        for (int index = 0; index < deckPerformance.Count; index++)
        {
            DeckPerformanceReadModel row = deckPerformance[index];
            rows[index] = new DeckPerformanceRow(row.Deck, row.CardsReviewed, row.Retention, row.Due, row.Progress);
        }

        return rows;
    }

    private static DailyStudyChartData CreateDailyStudyChartData(
        IReadOnlyList<ProgressAnalyticsActivityDayReadModel> dailyStudyActivity)
    {
        string[] labels = new string[dailyStudyActivity.Count];
        int[] cardsReviewed = new int[dailyStudyActivity.Count];
        for (int index = 0; index < dailyStudyActivity.Count; index++)
        {
            ProgressAnalyticsActivityDayReadModel row = dailyStudyActivity[index];
            labels[index] = FormatShortDate(row.Date);
            cardsReviewed[index] = row.CardsReviewed;
        }

        return new DailyStudyChartData(labels, cardsReviewed);
    }

    private static WeeklyReviewChartData CreateWeeklyReviewChartData(
        IReadOnlyList<WeeklyReviewOutcomeReadModel> weeklyReviewOutcomes)
    {
        string[] labels = new string[weeklyReviewOutcomes.Count];
        int[] again = new int[weeklyReviewOutcomes.Count];
        int[] hard = new int[weeklyReviewOutcomes.Count];
        int[] good = new int[weeklyReviewOutcomes.Count];
        int[] easy = new int[weeklyReviewOutcomes.Count];

        for (int index = 0; index < weeklyReviewOutcomes.Count; index++)
        {
            WeeklyReviewOutcomeReadModel row = weeklyReviewOutcomes[index];
            labels[index] = FormatWeekRange(row.WeekStart, row.WeekEnd).Replace(" - ", "\n");
            again[index] = row.Again;
            hard[index] = row.Hard;
            good[index] = row.Good;
            easy[index] = row.Easy;
        }

        return new WeeklyReviewChartData(labels, again, hard, good, easy);
    }

    private readonly record struct DailyStudyChartData(
        string[] Labels,
        int[] CardsReviewed);

    private readonly record struct WeeklyReviewChartData(
        string[] Labels,
        int[] Again,
        int[] Hard,
        int[] Good,
        int[] Easy);

    private static string FormatDays(int days)
    {
        return days == 1 ? "1 day" : string.Concat(days.ToString("N0", CultureInfo.InvariantCulture), " days");
    }

    private static string FormatDeckCount(int decks)
    {
        return decks == 1 ? "1 deck" : string.Concat(decks.ToString("N0", CultureInfo.InvariantCulture), " decks");
    }

    private static string FormatShortDate(DateOnly date)
    {
        return date.ToString("MMM d", CultureInfo.InvariantCulture);
    }

    private static string FormatWeekRange(DateOnly weekStart, DateOnly weekEnd)
    {
        return string.Concat(
            FormatShortDate(weekStart),
            " - ",
            FormatShortDate(weekEnd));
    }

    private static string FormatRecallLabel(ProgressAnalyticsRecallRating rating)
    {
        return rating.ToString();
    }

    private static string RecallBarColor(ProgressAnalyticsRecallRating rating)
    {
        return rating switch
        {
            ProgressAnalyticsRecallRating.Again => ThemeResourceLookup.ColorHex("RedBrush", "#DC2626"),
            ProgressAnalyticsRecallRating.Hard => ThemeResourceLookup.ColorHex("OrangeBrush", "#D97706"),
            ProgressAnalyticsRecallRating.Good => ThemeResourceLookup.ColorHex("BlueBrush", "#2563EB"),
            ProgressAnalyticsRecallRating.Easy => ThemeResourceLookup.ColorHex("GreenBrush", "#16A34A"),
            _ => ThemeResourceLookup.ColorHex("BlueBrush", "#2563EB")
        };
    }
}
