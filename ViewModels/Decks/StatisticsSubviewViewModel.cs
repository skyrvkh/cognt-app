using CogntApp.Models.Decks;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CogntApp.ViewModels.Decks;

public sealed class StatisticsSubviewViewModel
{
    public StatisticsSubviewViewModel()
    {
        ConfigureCharts();
    }

    public IReadOnlyList<DeckPerformanceRow> DeckPerformanceRows { get; } =
    [
        new("Biology 101", 342, 87, 24, 68),
        new("Anatomy Terms", 286, 83, 18, 54),
        new("Chemistry Review", 214, 79, 12, 47),
        new("Spanish Vocabulary", 198, 81, 9, 62),
        new("Physics Formulas", 154, 76, 11, 41)
    ];

    public IReadOnlyList<DailyStatisticsRow> DailyStatisticsRows { get; } =
    [
        new("Apr 22", 41, 72, 78),
        new("Apr 27", 63, 92, 81),
        new("May 2", 79, 134, 84),
        new("May 7", 47, 98, 76),
        new("May 12", 75, 148, 82),
        new("May 17", 64, 121, 85),
        new("May 21", 68, 126, 83)
    ];

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

    public IReadOnlyList<StudyStreakRow> StudyStreakRows { get; } =
    [
        new("Apr 22", [new(0), new(12), new(24), new(48), new(0), new(16), new(34)]),
        new("Apr 29", [new(8), new(0), new(42), new(29), new(55), new(61), new(18)]),
        new("May 6", [new(31), new(23), new(0), new(44), new(51), new(37), new(60)]),
        new("May 13", [new(0), new(17), new(36), new(40), new(26), new(53), new(47)]),
        new("May 20", [new(22), new(33), new(0), new(0), new(0), new(0), new(0)])
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

    private void ConfigureCharts()
    {
        DailyStatisticsSeries =
        [
            CreateColumnSeries("Cards Reviewed", DailyStatisticsRows.Select(row => row.CardsReviewed), "#2563EB", 18),
            CreateColumnSeries("Time Studied (min)", DailyStatisticsRows.Select(row => row.MinutesStudied), "#5867FF", 18)
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
                Values = CreateStudyStreakPoints(),
                HeatMap =
                [
                    SKColor.Parse("#EEF2FF").AsLvcColor(),
                    SKColor.Parse("#C7D2FE").AsLvcColor(),
                    SKColor.Parse("#A5B4FC").AsLvcColor(),
                    SKColor.Parse("#818CF8").AsLvcColor(),
                    SKColor.Parse("#5867FF").AsLvcColor()
                ]
            }
        ];

        StudyStreakXAxes =
        [
            new Axis
            {
                Labels = ["S", "M", "T", "W", "T", "F", "S"],
                LabelsPaint = ChartPaint("#98A2B3"),
                SeparatorsPaint = null,
                TextSize = 10
            }
        ];

        StudyStreakYAxes =
        [
            new Axis
            {
                Labels = StudyStreakRows.Select(row => row.Label).Reverse().ToArray(),
                LabelsPaint = ChartPaint("#98A2B3"),
                SeparatorsPaint = null,
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

    private WeightedPoint[] CreateStudyStreakPoints()
    {
        var points = new List<WeightedPoint>();
        var maxRowIndex = StudyStreakRows.Count - 1;

        for (var rowIndex = 0; rowIndex < StudyStreakRows.Count; rowIndex++)
        {
            var row = StudyStreakRows[rowIndex];
            var chartRow = maxRowIndex - rowIndex;

            for (var dayIndex = 0; dayIndex < row.Cells.Count; dayIndex++)
            {
                points.Add(new WeightedPoint(dayIndex, chartRow, row.Cells[dayIndex].Cards));
            }
        }

        return [.. points];
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
