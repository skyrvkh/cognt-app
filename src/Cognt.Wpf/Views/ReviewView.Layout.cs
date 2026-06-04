using System.Windows;
using System.Windows.Controls;

namespace CogntApp.Views;

public partial class ReviewView
{
    private const double HeaderHeight = 72;

    private Size lastLayoutSize = Size.Empty;

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

        StageRoot.MaxWidth = double.PositiveInfinity;
        StageRoot.VerticalAlignment = shortViewport || width < 900 ? VerticalAlignment.Top : VerticalAlignment.Center;
        StageContainer.Margin = ultraNarrow
            ? new Thickness(8, 10, 8, 14)
            : veryNarrow
                ? new Thickness(12, 14, 12, 18)
                : width < 1100
                    ? new Thickness(20, 18, 20, 22)
                    : new Thickness(36, 26, 36, 30);

        ReviewCardContentScrollViewer.Margin = ultraNarrow
            ? new Thickness(10, 4, 10, 0)
            : veryNarrow
                ? new Thickness(18, 4, 18, 0)
                : width < 1100
                    ? new Thickness(42, 4, 42, 0)
                    : new Thickness(84, 6, 84, 0);
        ReviewCardText.FontSize = ultraNarrow ? 24 : veryNarrow ? 28 : compact ? 31 : 34;
        ReviewCardText.LineHeight = ReviewCardText.FontSize * 1.28;
        ReviewCardRichText.FontSize = ReviewCardText.FontSize;
        ReviewCardImageContainer.MaxWidth = ultraNarrow ? 320 : veryNarrow ? 440 : width < 1180 ? 620 : 860;
        ReviewCardImageContainer.MaxHeight = ultraNarrow ? 190 : shortViewport ? 220 : compact ? 260 : 320;

        RevealPromptMessage.MaxWidth = ultraNarrow
            ? Math.Max(220, width - 72)
            : veryNarrow
                ? 320
                : 420;
        RevealAnswerButton.Width = ultraNarrow ? double.NaN : 320;
        RevealAnswerButton.MinWidth = ultraNarrow ? 0 : 180;

        ShortcutStrip.Visibility = ResponsiveLayout.VisibleWhen(!veryNarrow && !shortViewport);
        StageGapAfterSummaryRow.Height = new GridLength(compact ? 8 : 12);
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
        double summaryHeight = QueueSummaryStrip.ActualHeight + StageGapAfterSummaryRow.Height.Value;
        double shortcutHeight = ShortcutStrip.Visibility == Visibility.Visible
            ? 40 + StageGapBeforeShortcutsRow.Height.Value
            : 0;
        double ratingRows = RatingsPanel.Columns == 1 ? 4 : RatingsPanel.Columns == 2 ? 2 : 1;
        double ratingHeight = ratingRows * (ratingButtonHeight + ratingMargin.Top + ratingMargin.Bottom);
        double gaps = StageGapBeforeRatingsRow.Height.Value;
        double targetCardHeight = visibleContentHeight - verticalMargins - summaryHeight - shortcutHeight - ratingHeight - gaps;
        double minCardHeight = ultraNarrow ? 280 : veryNarrow || shortViewport ? 320 : 400;
        double maxCardHeight = Math.Max(minCardHeight, targetCardHeight);

        CardContext.Height = Clamp(targetCardHeight, minCardHeight, maxCardHeight);
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Min(Math.Max(value, minimum), maximum);
    }
}
