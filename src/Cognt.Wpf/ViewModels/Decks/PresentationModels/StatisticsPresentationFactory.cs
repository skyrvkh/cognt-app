using System.Globalization;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Models;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;

namespace CogntApp.ViewModels.Decks.PresentationModels;

internal static partial class StatisticsPresentationFactory
{
    private const int HeatmapWeekCount = 26;
    private const int DaysPerWeek = 7;
    private const int MovingAverageWindowDays = 7;

    internal static StatisticsPresentationState Create(ProgressAnalyticsReport report, DateOnly today)
    {
        IReadOnlyDictionary<DateOnly, int> reviewedByDate = report.StudyStreak
            .ToDictionary(day => day.Date, day => day.CardsReviewed);

        string[] dailyLabels = report.DailyStudyActivity
            .Select(row => FormatShortDate(row.Date))
            .ToArray();
        int[] dailyCardsReviewed = report.DailyStudyActivity
            .Select(row => row.CardsReviewed)
            .ToArray();
        string[] weeklyLabels = report.WeeklyReviewOutcomes
            .Select(row => FormatWeekRange(row.WeekStart, row.WeekEnd).Replace(" - ", "\n"))
            .ToArray();

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
            report.DeckPerformance
                .Select(row => new DeckPerformanceRow(row.Deck, row.CardsReviewed, row.Retention, row.Due, row.Progress))
                .ToArray(),
            [
                CreateColumnSeries("Cards Reviewed", dailyCardsReviewed, "#2563EB", 12),
                CreateMovingAverageSeries("Cards Reviewed 7-day avg", dailyCardsReviewed, "#0F766E")
            ],
            [CreateCategoryAxis(dailyLabels)],
            [CreateValueAxis()],
            CreateRecallPieSeries(report.RecallDistribution),
            [
                new HeatSeries<WeightedPoint>
                {
                    Name = "Cards",
                    Values = CreateStudyHeatmapPoints(today, reviewedByDate),
                    HeatMap =
                    [
                        SKColor.Parse("#F8FAFC").AsLvcColor(),
                        SKColor.Parse("#E0E7FF").AsLvcColor(),
                        SKColor.Parse("#C7D2FE").AsLvcColor(),
                        SKColor.Parse("#A5B4FC").AsLvcColor(),
                        SKColor.Parse("#5867FF").AsLvcColor()
                    ]
                }
            ],
            [CreateHeatmapXAxis(today)],
            [CreateHeatmapYAxis()],
            [
                CreateStackedColumnSeries("Again", report.WeeklyReviewOutcomes.Select(row => row.Again), "#DC2626"),
                CreateStackedColumnSeries("Hard", report.WeeklyReviewOutcomes.Select(row => row.Hard), "#D97706"),
                CreateStackedColumnSeries("Good", report.WeeklyReviewOutcomes.Select(row => row.Good), "#2563EB"),
                CreateStackedColumnSeries("Easy", report.WeeklyReviewOutcomes.Select(row => row.Easy), "#16A34A")
            ],
            [CreateCategoryAxis(weeklyLabels)],
            [CreateValueAxis()]);
    }

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
            ProgressAnalyticsRecallRating.Again => "#DC2626",
            ProgressAnalyticsRecallRating.Hard => "#D97706",
            ProgressAnalyticsRecallRating.Good => "#2563EB",
            ProgressAnalyticsRecallRating.Easy => "#16A34A",
            _ => "#2563EB"
        };
    }
}
