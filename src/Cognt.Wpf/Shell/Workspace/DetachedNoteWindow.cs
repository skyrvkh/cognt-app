using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CogntApp.Shell.Workspace;

internal sealed class DetachedNoteWindow : Window
{
    private readonly TextBlock titleText = new();
    private Point dragStart;
    private bool isDragging;

    internal DetachedNoteWindow(string title)
    {
        Title = title;
        Width = 920;
        Height = 680;
        MinWidth = 720;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Brushes.White;
        Icon = Application.Current.MainWindow?.Icon;

        Grid root = new();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Border header = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(225, 230, 240)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 7, 8, 7),
            Cursor = Cursors.SizeAll,
        };
        header.PreviewMouseLeftButtonDown += Header_PreviewMouseLeftButtonDown;
        header.PreviewMouseMove += Header_PreviewMouseMove;
        header.PreviewMouseLeftButtonUp += Header_PreviewMouseLeftButtonUp;

        Grid headerGrid = new();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        titleText.Text = title;
        titleText.VerticalAlignment = VerticalAlignment.Center;
        titleText.FontSize = 12;
        titleText.FontWeight = FontWeights.SemiBold;
        titleText.Foreground = new SolidColorBrush(Color.FromRgb(31, 36, 51));
        titleText.TextTrimming = TextTrimming.CharacterEllipsis;
        headerGrid.Children.Add(titleText);

        header.Child = headerGrid;
        root.Children.Add(header);

        EditorHost = new ContentControl();
        Grid.SetRow(EditorHost, 1);
        root.Children.Add(EditorHost);

        Content = root;
    }

    internal ContentControl EditorHost { get; }

    internal event EventHandler<DetachedNoteWindowDragCompletedEventArgs>? HeaderDragCompleted;

    internal void UpdateTitle(string title)
    {
        Title = title;
        titleText.Text = title;
    }

    private void Header_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(this);
        isDragging = true;
    }

    private void Header_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!isDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point current = e.GetPosition(this);
        if (Math.Abs(current.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        isDragging = false;
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        HeaderDragCompleted?.Invoke(
            this,
            new DetachedNoteWindowDragCompletedEventArgs(
                new Rect(Left, Top, ActualWidth, ActualHeight),
                ToScreenDip(PointToScreen(Mouse.GetPosition(this)))));
    }

    private void Header_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        isDragging = false;
    }

    private Point ToScreenDip(Point screenPoint)
    {
        PresentationSource? source = PresentationSource.FromVisual(this);
        return source?.CompositionTarget?.TransformFromDevice.Transform(screenPoint) ?? screenPoint;
    }
}

internal sealed class DetachedNoteWindowDragCompletedEventArgs(Rect screenBounds, Point dropScreenPoint) : EventArgs
{
    internal Rect ScreenBounds { get; } = screenBounds;

    internal Point DropScreenPoint { get; } = dropScreenPoint;
}
