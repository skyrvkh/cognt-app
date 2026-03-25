using System.Windows.Controls;
using System.Windows.Media;

namespace CogntApp.Views;

public partial class DashboardView : UserControl
{
    private const int HeatmapWeeks = 52;
    private const int HeatmapDaysPerWeek = 7;

    public DashboardView()
    {
        InitializeComponent();
        PopulateStudyHeatmap();
    }

    private void PopulateStudyHeatmap()
    {
        StudyHeatmapGrid.Children.Clear();

        for (int dayIndex = 0; dayIndex < HeatmapWeeks * HeatmapDaysPerWeek; dayIndex++)
        {
            int reviews = ReviewCountForDay(dayIndex);

            StudyHeatmapGrid.Children.Add(new Border
            {
                Width = 10,
                Height = 10,
                Margin = new System.Windows.Thickness(1.5),
                CornerRadius = new System.Windows.CornerRadius(3),
                Background = ReviewBrush(reviews),
                ToolTip = $"{reviews} reviews",
            });
        }
    }

    private static int ReviewCountForDay(int dayIndex)
    {
        int value = (dayIndex * 17 + dayIndex / 3 * 11 + 7) % 91;

        if (dayIndex % 11 == 0)
        {
            return 0;
        }

        if (dayIndex > 340)
        {
            value = Math.Max(value, 18);
        }

        return value;
    }

    private static Brush ReviewBrush(int reviews)
    {
        return reviews switch
        {
            0 => new SolidColorBrush(Color.FromRgb(238, 242, 255)),
            < 12 => new SolidColorBrush(Color.FromRgb(233, 237, 255)),
            < 24 => new SolidColorBrush(Color.FromRgb(221, 229, 255)),
            < 40 => new SolidColorBrush(Color.FromRgb(199, 212, 255)),
            < 62 => new SolidColorBrush(Color.FromRgb(174, 190, 255)),
            < 78 => new SolidColorBrush(Color.FromRgb(126, 146, 255)),
            _ => new SolidColorBrush(Color.FromRgb(89, 103, 255)),
        };
    }
}
