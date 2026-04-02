using CogntApp.Models.Decks;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Globalization;

namespace CogntApp.ViewModels.Decks;

public sealed class StatisticsSubviewViewModel
{
    private const int PreviousDayCount = 14;
    private const int HeatmapWeekCount = 26;
    private const int DaysPerWeek = 7;
    private const int MovingAverageWindowDays = 7;

    public StatisticsSubviewViewModel()
    {
        DateTime today = DateTime.Today;

        DailyStatisticsRows = CreatePreviousDailyStatisticsRows(today);
        ConfigureCharts(today);
    }

    public IReadOnlyList<DeckPerformanceRow> DeckPerformanceRows { get; } =
    [
        new("Biology 101", 342, 87, 24, 68),
        new("Anatomy Terms", 286, 83, 18, 54),
        new("Chemistry Review", 214, 79, 12, 47),
        new("Spanish Vocabulary", 198, 81, 9, 62),
        new("Physics Formulas", 154, 76, 11, 41)
    ];

    public IReadOnlyList<DailyStatisticsRow> DailyStatisticsRows { get; }

    public IReadOnlyList<RecallDistributionRow> RecallDistributionRows { get; } =
    [
        new("Again", 105, 8, "#DC2626"),
        new("Hard", 205, 16, "#D97706"),
        new("Good", 436, 34, "#2563EB"),
        new("Easy", 538, 42, "#16A34A")
    ];

    public IReadOnlyList<WeeklyOutcomeRow> WeeklyOutcomeRows { get; } =
    [
        new("Apr 22 - Apr 28", 9, 18, 33, 40),
        new("Apr 29 - May 5", 8, 17, 36, 39),
        new("May 6 - May 12", 7, 15, 34, 44),
        new("May 13 - May 19", 7, 16, 35, 42),
        new("May 20 - May 21", 8, 14, 32, 46)
    ];

    public ISeries[] DailyStatisticsSeries { get; private set; } = [];

    public Axis[] DailyStatisticsXAxes { get; private set; } = [];

    public Axis[] DailyStatisticsYAxes { get; private set; } = [];

    public ISeries[] RecallDistributionSeries { get; private set; } = [];

    public ISeries[] StudyStreakSeries { get; private set; } = [];

    public Axis[] StudyStreakXAxes { get; private set; } = [];

    public Axis[] StudyStreakYAxes { get; private set; } = [];

    public ISeries[] WeeklyOutcomeSeries { get; private set; } = [];

    public Axis[] WeeklyOutcomeXAxes { get; private set; } = [];

    public Axis[] WeeklyOutcomeYAxes { get; private set; } = [];

    private void ConfigureCharts(DateTime today)
    {
        DailyStatisticsSeries =
        [
            CreateColumnSeries("Cards Reviewed", DailyStatisticsRows.Select(row => row.CardsReviewed), "#2563EB", 12),
            CreateColumnSeries("Time Studied (min)", DailyStatisticsRows.Select(row => row.MinutesStudied), "#5867FF", 12),
            CreateMovingAverageSeries(
                "Cards Reviewed 7-day avg",
                DailyStatisticsRows.Select(row => row.CardsReviewed),
                "#0F766E"),
            CreateMovingAverageSeries(
                "Time Studied 7-day avg",
                DailyStatisticsRows.Select(row => row.MinutesStudied),
                "#B45309")
        ];

        DailyStatisticsXAxes = [CreateCategoryAxis(DailyStatisticsRows.Select(row => row.Date).ToArray())];
        DailyStatisticsYAxes =
        [
            new Axis
            {
                MinLimit = 0,
                LabelsPaint = ChartPaint("#667085"),
                SeparatorsPaint = ChartPaint("#ECF0F6"),
                TextSize = 11
            }
        ];

        RecallDistributionSeries = CreateRecallPieSeries();

        StudyStreakSeries =
        [
            new HeatSeries<WeightedPoint>
            {
                Name = "Cards",
                Values = CreateStudyHeatmapPoints(today),
                HeatMap =
                [
                    SKColor.Parse("#F8FAFC").AsLvcColor(),
                    SKColor.Parse("#E0E7FF").AsLvcColor(),
                    SKColor.Parse("#C7D2FE").AsLvcColor(),
                    SKColor.Parse("#A5B4FC").AsLvcColor(),
                    SKColor.Parse("#5867FF").AsLvcColor()
                ]
            }
        ];

        StudyStreakXAxes =
        [
            new Axis
            {
                Labels = CreateHeatmapWeekLabels(today),
                LabelsPaint = ChartPaint("#98A2B3"),
                SeparatorsPaint = null,
                MinLimit = -0.5,
                MaxLimit = HeatmapWeekCount - 0.5,
                TextSize = 10
            }
        ];

        StudyStreakYAxes =
        [
            new Axis
            {
                Labels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"],
                LabelsPaint = ChartPaint("#98A2B3"),
                SeparatorsPaint = null,
                MinLimit = -0.5,
                MaxLimit = DaysPerWeek - 0.5,
                TextSize = 10
            }
        ];

        WeeklyOutcomeSeries =
        [
            CreateStackedColumnSeries("Again", WeeklyOutcomeRows.Select(row => row.Again), "#DC2626"),
            CreateStackedColumnSeries("Hard", WeeklyOutcomeRows.Select(row => row.Hard), "#D97706"),
            CreateStackedColumnSeries("Good", WeeklyOutcomeRows.Select(row => row.Good), "#2563EB"),
            CreateStackedColumnSeries("Easy", WeeklyOutcomeRows.Select(row => row.Easy), "#16A34A")
        ];

        WeeklyOutcomeXAxes =
        [
            CreateCategoryAxis(WeeklyOutcomeRows
                .Select(row => row.Week.Replace(" - ", "\n"))
                .ToArray())
        ];

        WeeklyOutcomeYAxes =
        [
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 100,
                LabelsPaint = ChartPaint("#667085"),
                SeparatorsPaint = ChartPaint("#ECF0F6"),
                TextSize = 11
            }
        ];
    }

    private static IReadOnlyList<DailyStatisticsRow> CreatePreviousDailyStatisticsRows(DateTime today)
    {
        int[] cardCounts = [38, 44, 59, 32, 74, 86, 41, 57, 68, 50, 92, 77, 63, 81];
        int[] studiedMinutes = [42, 54, 71, 38, 89, 104, 46, 66, 83, 60, 112, 91, 75, 98];

        return Enumerable
            .Range(0, PreviousDayCount)
            .Select(index =>
            {
                DateTime date = today.Date.AddDays(index - (PreviousDayCount - 1));

                return new DailyStatisticsRow(
                    date.ToString("MMM d", CultureInfo.InvariantCulture),
                    cardCounts[index],
                    studiedMinutes[index]);
            })
            .ToArray();
    }

    private static WeightedPoint[] CreateStudyHeatmapPoints(DateTime today)
    {
        var points = new List<WeightedPoint>();
        DateTime firstWeekStart = GetWeekStart(today).AddDays(-(HeatmapWeekCount - 1) * DaysPerWeek);

        for (var weekIndex = 0; weekIndex < HeatmapWeekCount; weekIndex++)
        {
            DateTime weekStart = firstWeekStart.AddDays(weekIndex * DaysPerWeek);

            for (var dayIndex = 0; dayIndex < DaysPerWeek; dayIndex++)
            {
                DateTime date = weekStart.AddDays(dayIndex);
                int cards = date > today.Date ? 0 : CalculateStudyCards(date);

                points.Add(new WeightedPoint(weekIndex, dayIndex, cards));
            }
        }

        return [.. points];
    }

    private static string[] CreateHeatmapWeekLabels(DateTime today)
    {
        DateTime firstWeekStart = GetWeekStart(today).AddDays(-(HeatmapWeekCount - 1) * DaysPerWeek);
        var labels = new string[HeatmapWeekCount];

        for (var weekIndex = 0; weekIndex < HeatmapWeekCount; weekIndex++)
        {
            DateTime weekStart = firstWeekStart.AddDays(weekIndex * DaysPerWeek);
            labels[weekIndex] = weekIndex % 4 == 0 || weekIndex == HeatmapWeekCount - 1
                ? weekStart.ToString("MMM d", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        return labels;
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        int dayOffset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + DaysPerWeek) % DaysPerWeek;
        return date.Date.AddDays(-dayOffset);
    }

    private static int CalculateStudyCards(DateTime date)
    {
        int seed = (date.DayOfYear * 17 + (int)date.DayOfWeek * 11) % 68;

        return seed < 12 ? 0 : seed + 8;
    }

    private ISeries[] CreateRecallPieSeries()
    {
        return RecallDistributionRows
            .Select(row => new PieSeries<int>
            {
                Name = $"{row.Label} {row.PercentLabel}",
                Values = [row.Count],
                Fill = ChartPaint(row.BarColor)
            })
            .ToArray<ISeries>();
    }

    private static Axis CreateCategoryAxis(string[] labels)
    {
        return new Axis
        {
            Labels = labels,
            LabelsPaint = ChartPaint("#98A2B3"),
            SeparatorsPaint = null,
            TextSize = 11
        };
    }

    private static ColumnSeries<int> CreateColumnSeries(
        string name,
        IEnumerable<int> values,
        string color,
        double maxBarWidth)
    {
        return new ColumnSeries<int>
        {
            Name = name,
            Values = values.ToArray(),
            Fill = ChartPaint(color),
            Stroke = null,
            MaxBarWidth = maxBarWidth,
            Padding = 8,
            Rx = 4,
            Ry = 4
        };
    }

    private static LineSeries<double> CreateMovingAverageSeries(
        string name,
        IEnumerable<int> values,
        string color)
    {
        SolidColorPaint stroke = ChartPaint(color);
        stroke.StrokeThickness = 3;

        return new LineSeries<double>
        {
            Name = name,
            Values = CalculateMovingAverage(values, MovingAverageWindowDays),
            Fill = null,
            Stroke = stroke,
            GeometryFill = ChartPaint(color),
            GeometrySize = 7,
            GeometryStroke = ChartPaint("#FFFFFF"),
            LineSmoothness = 0.35
        };
    }

    private static double[] CalculateMovingAverage(IEnumerable<int> values, int windowSize)
    {
        int[] source = values.ToArray();
        var averages = new double[source.Length];
        double rollingTotal = 0;

        for (int index = 0; index < source.Length; index++)
        {
            rollingTotal += source[index];

            if (index >= windowSize)
            {
                rollingTotal -= source[index - windowSize];
            }

            int divisor = Math.Min(index + 1, windowSize);
            averages[index] = Math.Round(rollingTotal / divisor, 1);
        }

        return averages;
    }

    private static StackedColumnSeries<int> CreateStackedColumnSeries(
        string name,
        IEnumerable<int> values,
        string color)
    {
        return new StackedColumnSeries<int>
        {
            Name = name,
            Values = values.ToArray(),
            Fill = ChartPaint(color),
            Stroke = null,
            MaxBarWidth = 46,
            Padding = 10,
            Rx = 4,
            Ry = 4
        };
    }

    private static SolidColorPaint ChartPaint(string color)
    {
        return new SolidColorPaint(SKColor.Parse(color));
    }
}
