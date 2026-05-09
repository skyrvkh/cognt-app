using System.Globalization;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;

namespace CogntApp.ViewModels.Decks.PresentationModels;

internal static partial class StatisticsPresentationFactory
{
    private static WeightedPoint[] CreateStudyHeatmapPoints(
        DateOnly today,
        IReadOnlyDictionary<DateOnly, int> reviewedByDate)
    {
        var points = new List<WeightedPoint>();
        DateOnly firstWeekStart = GetFirstHeatmapWeekStart(today);

        for (var weekIndex = 0; weekIndex < HeatmapWeekCount; weekIndex++)
        {
            DateOnly weekStart = firstWeekStart.AddDays(weekIndex * DaysPerWeek);

            for (var dayIndex = 0; dayIndex < DaysPerWeek; dayIndex++)
            {
                DateOnly date = weekStart.AddDays(dayIndex);
                reviewedByDate.TryGetValue(date, out int cards);
                points.Add(new WeightedPoint(weekIndex, dayIndex, cards));
            }
        }

        return [.. points];
    }

    private static Axis CreateHeatmapXAxis(DateOnly today)
    {
        return new Axis
        {
            Labels = CreateHeatmapWeekLabels(today),
            LabelsPaint = ChartPaint("#98A2B3"),
            SeparatorsPaint = null,
            MinLimit = -0.5,
            MaxLimit = HeatmapWeekCount - 0.5,
            TextSize = 10
        };
    }

    private static Axis CreateHeatmapYAxis()
    {
        return new Axis
        {
            Labels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"],
            LabelsPaint = ChartPaint("#98A2B3"),
            SeparatorsPaint = null,
            MinLimit = -0.5,
            MaxLimit = DaysPerWeek - 0.5,
            TextSize = 10
        };
    }

    private static string[] CreateHeatmapWeekLabels(DateOnly today)
    {
        DateOnly firstWeekStart = GetFirstHeatmapWeekStart(today);
        var labels = new string[HeatmapWeekCount];

        for (var weekIndex = 0; weekIndex < HeatmapWeekCount; weekIndex++)
        {
            DateOnly weekStart = firstWeekStart.AddDays(weekIndex * DaysPerWeek);
            labels[weekIndex] = weekIndex % 4 == 0 || weekIndex == HeatmapWeekCount - 1
                ? weekStart.ToString("MMM d", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        return labels;
    }

    private static DateOnly GetFirstHeatmapWeekStart(DateOnly today)
    {
        return GetWeekStart(today).AddDays(-(HeatmapWeekCount - 1) * DaysPerWeek);
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        int dayOffset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + DaysPerWeek) % DaysPerWeek;
        return date.AddDays(-dayOffset);
    }
}
