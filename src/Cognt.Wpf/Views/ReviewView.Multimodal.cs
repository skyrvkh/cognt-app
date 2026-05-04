using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

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
            FlashcardSemanticStyle.CurrentTarget => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D4ED8")),
            FlashcardSemanticStyle.SiblingTarget => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B45309")),
            FlashcardSemanticStyle.HiddenTarget => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
            FlashcardSemanticStyle.ResolvedTarget => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937")),
        };
        element.Background = isHidden
            ? style switch
            {
                FlashcardSemanticStyle.CurrentTarget => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE")),
                FlashcardSemanticStyle.SiblingTarget => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")),
                FlashcardSemanticStyle.HiddenTarget => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
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
        (string fill, string stroke) = style switch
        {
            FlashcardSemanticStyle.CurrentTarget => ("#BFD6FF", "#1D4ED8"),
            FlashcardSemanticStyle.SiblingTarget => ("#FDE68A", "#B45309"),
            FlashcardSemanticStyle.HiddenTarget => ("#D1D5DB", "#4B5563"),
            FlashcardSemanticStyle.ResolvedTarget => ("#BBF7D0", "#15803D"),
            _ => ("#D1D5DB", "#4B5563"),
        };

        shape.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fill))
        {
            Opacity = isHidden ? 0.8 : 0.35,
        };
        shape.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(stroke));
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
