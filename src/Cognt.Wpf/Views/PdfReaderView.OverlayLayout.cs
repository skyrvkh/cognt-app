using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CogntApp.Views;

public partial class PdfReaderView
{
    private static Border CreateOverlay(
        Canvas canvas,
        double leftFraction,
        double topFraction,
        double widthFraction,
        double heightFraction,
        Brush background,
        Brush borderBrush,
        double borderThickness)
    {
        Border overlay = new()
        {
            Width = Math.Max(2d, canvas.Width * widthFraction),
            Height = Math.Max(2d, canvas.Height * heightFraction),
            Background = background,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(borderThickness),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(overlay, Math.Max(0d, canvas.Width * leftFraction));
        Canvas.SetTop(overlay, Math.Max(0d, canvas.Height * topFraction));
        Panel.SetZIndex(overlay, 2);
        canvas.Children.Add(overlay);
        return overlay;
    }

    private static void AddWordOverlay(
        Canvas canvas,
        PdfPageStudyData page,
        PdfTextWord word,
        Brush background,
        Brush border,
        double borderThickness)
    {
        double left = Math.Clamp(word.Left / page.Width, 0d, 1d);
        double top = Math.Clamp((page.Height - word.Top) / page.Height, 0d, 1d);
        double width = Math.Clamp((word.Right - word.Left) / page.Width, 0d, 1d - left);
        double height = Math.Clamp((word.Top - word.Bottom) / page.Height, 0d, 1d - top);
        _ = CreateOverlay(canvas, left, top, width, height, background, border, borderThickness);
    }
}
