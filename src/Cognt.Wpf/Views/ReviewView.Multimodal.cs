using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using CogntApp.Design;

namespace CogntApp.Views;

public partial class ReviewView
{
    private MultimodalClozeReadModel? currentMultimodalContent;

    private void LoadCurrentCard()
    {
        if (currentCard is null)
        {
            throw new InvalidOperationException("There is no review card loaded.");
        }

        if (TryLoadMultimodalCard())
        {
            return;
        }

        currentMultimodalContent = null;
        ReviewCardMultimodalHost.Visibility = Visibility.Collapsed;
        ReviewCardText.Visibility = Visibility.Visible;
        ReviewCardText.Text = isAnswerVisible ? currentCard.BackText : currentCard.FrontText;
        ReviewCardImage.Source = null;
        ReviewCardImageContainer.Visibility = Visibility.Collapsed;
        ReviewCardMaskCanvas.Children.Clear();
    }

    private bool TryLoadMultimodalCard()
    {
        if (currentCard is null)
        {
            return false;
        }

        MultimodalClozeReadModel? content = isAnswerVisible
            ? currentCard.BackMultimodalContent
            : currentCard.FrontMultimodalContent;
        if (content is null)
        {
            return false;
        }

        currentMultimodalContent = content;
        ReviewCardText.Visibility = Visibility.Collapsed;
        ReviewCardMultimodalHost.Visibility = Visibility.Visible;
        RenderMultimodalText(content);
        RenderMultimodalImage(content.ImageOcclusion);
        return true;
    }

    private void RenderMultimodalText(MultimodalClozeReadModel content)
    {
        FlowDocument document = new()
        {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Center,
            Background = Brushes.Transparent,
        };

        Paragraph paragraph = new()
        {
            Margin = new Thickness(0),
            TextAlignment = TextAlignment.Center,
        };

        foreach (MultimodalTextSegmentReadModel segment in content.TextSegments)
        {
            Run run = new(segment.Text);
            ApplySegmentStyle(run, segment.Style, segment.IsHidden);
            paragraph.Inlines.Add(run);
        }

        document.Blocks.Add(paragraph);
        ReviewCardRichText.Document = document;
    }

    private void ApplySegmentStyle(TextElement element, FlashcardSemanticStyle style, bool isHidden)
    {
        element.Foreground = style switch
        {
            FlashcardSemanticStyle.CurrentTarget => ThemeResourceLookup.SolidBrush("BlueBrush", "#1D4ED8"),
            FlashcardSemanticStyle.SiblingTarget => ThemeResourceLookup.SolidBrush("WarningBrush", "#B45309"),
            FlashcardSemanticStyle.HiddenTarget => ThemeResourceLookup.SolidBrush("DimBrush", "#374151"),
            FlashcardSemanticStyle.ResolvedTarget => ThemeResourceLookup.SolidBrush("InkBrush", "#1F2937"),
            _ => ThemeResourceLookup.SolidBrush("InkBrush", "#1F2937"),
        };
        element.Background = isHidden
            ? style switch
            {
                FlashcardSemanticStyle.CurrentTarget => ThemeResourceLookup.SolidBrush("NoteFillBrush", "#DBEAFE"),
                FlashcardSemanticStyle.SiblingTarget => ThemeResourceLookup.SolidBrush("WarningSurfaceBrush", "#FEF3C7"),
                FlashcardSemanticStyle.HiddenTarget => ThemeResourceLookup.SolidBrush("DisabledSurfaceBrush", "#E5E7EB"),
                _ => ThemeResourceLookup.SolidBrush("DisabledSurfaceBrush", "#E5E7EB"),
            }
            : Brushes.Transparent;
        element.FontWeight = style == FlashcardSemanticStyle.CurrentTarget
            ? FontWeights.SemiBold
            : FontWeights.Normal;
    }

    private void RenderMultimodalImage(ImageOcclusionReadModel? imageOcclusion)
    {
        ReviewCardMaskCanvas.Children.Clear();
        ReviewCardImage.Source = null;

        if (imageOcclusion is null
            || string.IsNullOrWhiteSpace(imageOcclusion.ImageFullPath)
            || !File.Exists(imageOcclusion.ImageFullPath))
        {
            ReviewCardImageContainer.Visibility = Visibility.Collapsed;
            return;
        }

        BitmapImage bitmap = new();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.UriSource = new Uri(imageOcclusion.ImageFullPath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        ReviewCardImage.Source = bitmap;
        ReviewCardImageContainer.Visibility = Visibility.Visible;
        ReviewCardImageHost.UpdateLayout();
        RenderMaskCanvas(imageOcclusion);
    }

    private void RenderMaskCanvas(ImageOcclusionReadModel? imageOcclusion)
    {
        ReviewCardMaskCanvas.Children.Clear();
        if (imageOcclusion is null || ReviewCardImage.Source is null)
        {
            return;
        }

        double width = ReviewCardImage.ActualWidth;
        double height = ReviewCardImage.ActualHeight;
        if (width <= 0d || height <= 0d)
        {
            return;
        }

        ReviewCardMaskCanvas.Width = width;
        ReviewCardMaskCanvas.Height = height;

        foreach (ImageMaskReadModel mask in imageOcclusion.Masks)
        {
            Shape shape = mask.Shape == ImageMaskShape.Ellipse
                ? CreateEllipseMask(mask, width, height)
                : CreateRectMask(mask, width, height);
            ApplyMaskStyle(shape, mask.Style, mask.IsHidden);
            ReviewCardMaskCanvas.Children.Add(shape);
        }
    }

    private static Shape CreateRectMask(ImageMaskReadModel mask, double width, double height)
    {
        Rectangle rectangle = new()
        {
            Width = mask.Width * width,
            Height = mask.Height * height,
            RadiusX = 8,
            RadiusY = 8,
        };
        Canvas.SetLeft(rectangle, mask.X * width);
        Canvas.SetTop(rectangle, mask.Y * height);
        return rectangle;
    }

    private static Shape CreateEllipseMask(ImageMaskReadModel mask, double width, double height)
    {
        double cx = mask.Cx ?? 0d;
        double cy = mask.Cy ?? 0d;
        double rx = mask.Rx ?? 0d;
        double ry = mask.Ry ?? 0d;
        Ellipse ellipse = new()
        {
            Width = rx * width * 2d,
            Height = ry * height * 2d,
        };
        Canvas.SetLeft(ellipse, (cx - rx) * width);
        Canvas.SetTop(ellipse, (cy - ry) * height);
        return ellipse;
    }

    private static void ApplyMaskStyle(Shape shape, FlashcardSemanticStyle style, bool isHidden)
    {
        (SolidColorBrush fill, SolidColorBrush stroke) = style switch
        {
            FlashcardSemanticStyle.CurrentTarget => (
                ThemeResourceLookup.SolidBrush("NoteFillBrush", "#BFD6FF"),
                ThemeResourceLookup.SolidBrush("BlueBrush", "#1D4ED8")),
            FlashcardSemanticStyle.SiblingTarget => (
                ThemeResourceLookup.SolidBrush("WarningSurfaceBrush", "#FDE68A"),
                ThemeResourceLookup.SolidBrush("WarningBrush", "#B45309")),
            FlashcardSemanticStyle.HiddenTarget => (
                ThemeResourceLookup.SolidBrush("DisabledSurfaceBrush", "#D1D5DB"),
                ThemeResourceLookup.SolidBrush("DimBrush", "#4B5563")),
            FlashcardSemanticStyle.ResolvedTarget => (
                ThemeResourceLookup.SolidBrush("SuccessSurfaceBrush", "#BBF7D0"),
                ThemeResourceLookup.SolidBrush("SuccessBrush", "#15803D")),
            _ => (
                ThemeResourceLookup.SolidBrush("DisabledSurfaceBrush", "#D1D5DB"),
                ThemeResourceLookup.SolidBrush("DimBrush", "#4B5563")),
        };

        fill.Opacity = isHidden ? 0.8 : 0.35;
        shape.Fill = fill;
        shape.Stroke = stroke;
        shape.StrokeThickness = style == FlashcardSemanticStyle.CurrentTarget ? 3 : 2;
    }

    private void ReviewCardImageHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (currentMultimodalContent?.ImageOcclusion is not null)
        {
            RenderMaskCanvas(currentMultimodalContent.ImageOcclusion);
        }
    }
}
