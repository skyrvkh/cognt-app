using System.Windows;
using System.Windows.Controls;

namespace CogntApp.Views;

public partial class ReviewView : UserControl
{
    private const double HeaderHeight = 72;

    private static readonly ReviewSampleCard[] SampleCards =
    [
        new(
            "What organelle produces most ATP in a eukaryotic cell?",
            "**Mitochondria** produce most ATP through cellular respiration."),
        new(
            "What molecule carries genetic instructions in cells?",
            "**DNA** carries genetic instructions used for growth, function, and reproduction."),
        new(
            "What does a pH below 7 indicate?",
            "A pH below 7 indicates an **acidic** solution."),
        new(
            "Which trade route connected East Asia with Europe?",
            "The **Silk Road** connected East Asia with Europe through overland trade networks."),
        new(
            "What does immutability mean?",
            "An immutable value cannot be changed after it is created.")
    ];

    private int currentCardIndex;
    private bool isAnswerVisible;
    private bool isDisposed;
    private Size lastLayoutSize = Size.Empty;

    public event EventHandler? ExitReviewRequested;

    public Func<string, Task>? RenderCardAsync { get; set; }

    public ContentControl CardRendererHostControl => CardRendererHost;

    public ReviewView()
    {
        InitializeComponent();
    }

    private void ReviewView_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void ReviewView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    public async Task StartReviewAsync()
    {
        ThrowIfDisposed();
        currentCardIndex = 0;
        isAnswerVisible = false;
        UpdateCardState();
        await LoadCurrentCardAsync();
    }

    private async void RevealAnswer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await RevealAnswerAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The card answer could not be shown: {ex.Message}",
                "Review Renderer Not Ready",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ExitReview_Click(object sender, RoutedEventArgs e)
    {
        ExitReviewRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void RatingButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await MoveToNextCardAsync();
        }
        catch (Exception ex)
        {
            ShowReviewError("The review card could not be advanced", ex);
        }
    }

    public void DisposeReview()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        RenderCardAsync = null;
        CardRendererHost.Content = null;
    }

    private async Task RevealAnswerAsync()
    {
        if (isAnswerVisible)
        {
            return;
        }

        isAnswerVisible = true;
        UpdateCardState();
        await LoadCurrentCardAsync();
    }

    private async Task MoveToNextCardAsync()
    {
        currentCardIndex = (currentCardIndex + 1) % SampleCards.Length;
        isAnswerVisible = false;
        UpdateCardState();
        await LoadCurrentCardAsync();
    }

    private Task LoadCurrentCardAsync()
    {
        ReviewSampleCard card = SampleCards[currentCardIndex];
        return LoadCardAsync(isAnswerVisible ? card.BackMarkdown : card.FrontMarkdown);
    }

    private async Task LoadCardAsync(string markdown)
    {
        ThrowIfDisposed();

        if (RenderCardAsync is null)
        {
            throw new InvalidOperationException("No shared BlockSuite renderer is attached.");
        }

        await RenderCardAsync(markdown);
    }

    private void UpdateCardState()
    {
        CardSideLabel.Text = isAnswerVisible ? "BACK" : "FRONT";
        RevealPromptPanel.Visibility = isAnswerVisible ? Visibility.Collapsed : Visibility.Visible;
        AnswerStatePanel.Visibility = isAnswerVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyResponsiveLayout()
    {
        if (!ResponsiveLayout.TryBeginSizeLayout(ActualWidth, ActualHeight, ref lastLayoutSize))
        {
            return;
        }

        double width = lastLayoutSize.Width;
        double height = lastLayoutSize.Height;
        bool narrow = width < 1040;
        bool compact = width < 1240 || height < 860;
        bool veryNarrow = width < 760;
        bool ultraNarrow = width < 480;
        bool shortViewport = height < 700;
        bool veryShortViewport = height < 620;

        HeaderTabs.Visibility = ResponsiveLayout.VisibleWhen(!veryNarrow);
        HeaderTitle.FontSize = ultraNarrow ? 19 : 22;
        ExitReviewButton.Width = ultraNarrow ? 44 : 128;
        ExitReviewButton.Padding = ultraNarrow ? new Thickness(0) : new Thickness(14, 0, 14, 0);
        ExitReviewGlyph.Margin = ultraNarrow ? new Thickness(0) : new Thickness(0, 0, 8, 0);
        ExitReviewLabel.Visibility = ResponsiveLayout.VisibleWhen(!ultraNarrow);

        StageRoot.MaxWidth = ultraNarrow
            ? double.PositiveInfinity
            : width >= 1500
                ? 1520
                : width >= 1180
                    ? 1280
                    : 980;
        StageRoot.VerticalAlignment = shortViewport || width < 900 ? VerticalAlignment.Top : VerticalAlignment.Center;
        StageContainer.Margin = ultraNarrow
            ? new Thickness(8, 10, 8, 14)
            : veryNarrow
                ? new Thickness(12, 14, 12, 18)
                : width < 1100
                    ? new Thickness(20, 18, 20, 22)
                    : new Thickness(36, 26, 36, 30);

        CardRendererHost.Margin = ultraNarrow
            ? new Thickness(10, 4, 10, 0)
            : veryNarrow
                ? new Thickness(18, 4, 18, 0)
                : width < 1100
                    ? new Thickness(42, 4, 42, 0)
                    : new Thickness(84, 6, 84, 0);

        RevealPromptMessage.MaxWidth = ultraNarrow
            ? Math.Max(220, width - 72)
            : veryNarrow
                ? 320
                : 420;
        RevealAnswerButton.Width = ultraNarrow ? double.NaN : 320;
        RevealAnswerButton.MinWidth = ultraNarrow ? 0 : 180;

        ShortcutStrip.Visibility = ResponsiveLayout.VisibleWhen(!veryNarrow && !shortViewport);
        StageGapBeforeShortcutsRow.Height = new GridLength(
            ShortcutStrip.Visibility == Visibility.Visible
                ? compact
                    ? 10
                    : 14
                : 0);
        StageGapBeforeRatingsRow.Height = new GridLength(compact ? 10 : 16);
        RatingsPanel.Columns = ultraNarrow ? 1 : veryNarrow ? 2 : 4;

        double ratingButtonHeight = ultraNarrow ? 82 : compact ? 94 : 108;
        Thickness ratingMargin = ultraNarrow ? new Thickness(0, 4, 0, 4) : narrow ? new Thickness(5) : new Thickness(6);
        foreach (UIElement child in RatingsPanel.Children)
        {
            if (child is Button button)
            {
                button.Height = ratingButtonHeight;
                button.Margin = ratingMargin;
                button.Padding = ultraNarrow ? new Thickness(14, 0, 14, 0) : new Thickness(20, 0, 20, 0);
            }
        }

        CardHeaderRow.Height = new GridLength(veryShortViewport ? 40 : shortViewport ? 52 : compact ? 64 : 76);
        CardFooterRow.Height = new GridLength(ultraNarrow ? 118 : shortViewport ? 104 : compact ? 112 : 124);

        double visibleContentHeight = Math.Max(0, height - HeaderHeight);
        double verticalMargins = StageContainer.Margin.Top + StageContainer.Margin.Bottom;
        double shortcutHeight = ShortcutStrip.Visibility == Visibility.Visible
            ? 40 + StageGapBeforeShortcutsRow.Height.Value
            : 0;
        double ratingRows = RatingsPanel.Columns == 1 ? 4 : RatingsPanel.Columns == 2 ? 2 : 1;
        double ratingHeight = ratingRows * (ratingButtonHeight + ratingMargin.Top + ratingMargin.Bottom);
        double gaps = StageGapBeforeRatingsRow.Height.Value;
        double targetCardHeight = visibleContentHeight - verticalMargins - shortcutHeight - ratingHeight - gaps;
        double minCardHeight = ultraNarrow ? 280 : veryNarrow || shortViewport ? 320 : 400;
        double maxCardHeight = veryShortViewport ? 520 : height >= 980 ? 720 : 660;

        CardFrame.Height = Clamp(targetCardHeight, minCardHeight, maxCardHeight);
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Min(Math.Max(value, minimum), maximum);
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(ReviewView));
        }
    }

    private static void ShowReviewError(string title, Exception ex)
    {
        MessageBox.Show(
            $"{title}: {ex.Message}",
            "Review",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private sealed record ReviewSampleCard(
        string FrontMarkdown,
        string BackMarkdown);
}
