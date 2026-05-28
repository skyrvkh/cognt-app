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
        ISeries[] series = new ISeries[recallRows.Count];
        for (int index = 0; index < recallRows.Count; index++)
        {
            RecallDistributionReadModel row = recallRows[index];
            series[index] = new PieSeries<int>
            {
                Name = $"{FormatRecallLabel(row.Rating)} {row.Percent.ToString(CultureInfo.InvariantCulture)}%",
                Values = [row.Count],
                Fill = ChartPaint(RecallBarColor(row.Rating))
            };
        }

        return series;
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
        int[] values,
        string color,
        double maxBarWidth)
    {
        return new ColumnSeries<int>
        {
            Name = name,
            Values = values,
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
        IReadOnlyList<int> values,
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

    private static double[] CalculateMovingAverage(IReadOnlyList<int> values, int windowSize)
    {
        var averages = new double[values.Count];
        double rollingTotal = 0;

        for (int index = 0; index < values.Count; index++)
        {
            rollingTotal += values[index];

            if (index >= windowSize)
            {
                rollingTotal -= values[index - windowSize];
            }

            int divisor = Math.Min(index + 1, windowSize);
            averages[index] = Math.Round(rollingTotal / divisor, 1);
        }

        return averages;
    }

    private static StackedColumnSeries<int> CreateStackedColumnSeries(
        string name,
        int[] values,
        string color)
    {
        return new StackedColumnSeries<int>
        {
            Name = name,
            Values = values,
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
