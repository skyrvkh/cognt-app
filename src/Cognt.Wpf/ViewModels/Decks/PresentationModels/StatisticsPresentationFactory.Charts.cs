using System.Globalization;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;
using CogntApp.Design;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CogntApp.ViewModels.Decks.PresentationModels;

internal static partial class StatisticsPresentationFactory
{
    private static ISeries[] CreateRecallPieSeries(IReadOnlyList<RecallDistributionReadModel> recallRows)
    {
        return recallRows
            .Select(row => new PieSeries<int>
            {
                Name = $"{FormatRecallLabel(row.Rating)} {row.Percent.ToString(CultureInfo.InvariantCulture)}%",
                Values = [row.Count],
                Fill = ChartPaint(RecallBarColor(row.Rating))
            })
            .ToArray<ISeries>();
    }

    private static Axis CreateCategoryAxis(string[] labels)
    {
        return new Axis
        {
            Labels = labels,
            LabelsPaint = ChartPaint(ThemeResourceLookup.ColorHex("DimBrush", "#98A2B3")),
            SeparatorsPaint = null,
            TextSize = 11
        };
    }

    private static Axis CreateValueAxis()
    {
        return new Axis
        {
            MinLimit = 0,
            LabelsPaint = ChartPaint(ThemeResourceLookup.ColorHex("MutedBrush", "#667085")),
            SeparatorsPaint = ChartPaint(ThemeResourceLookup.ColorHex("LineSoftBrush", "#ECF0F6")),
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
            GeometryStroke = ChartPaint(ThemeResourceLookup.ColorHex("PanelBrush", "#FFFFFF")),
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
