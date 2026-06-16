using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CogntApp.Views;

public partial class PdfReaderView
{
    private void RenderAnnotations(Canvas canvas, PdfPageStudyData page)
    {
        foreach (PdfStudyAnnotation annotation in annotations.Where(annotation => annotation.PageNumber == page.PageNumber))
        {
            Brush fill = annotation.Kind == PdfStudyAnnotationKind.Comment
                ? new SolidColorBrush(Color.FromArgb(95, 124, 58, 237))
                : new SolidColorBrush(Color.FromArgb(100, 250, 204, 21));
            Brush border = annotation.Kind == PdfStudyAnnotationKind.Comment
                ? new SolidColorBrush(Color.FromArgb(210, 109, 40, 217))
                : new SolidColorBrush(Color.FromArgb(210, 202, 138, 4));
            Border overlay = CreateOverlay(canvas, annotation.Left, annotation.Top, annotation.Width, annotation.Height, fill, border, annotation.Kind == PdfStudyAnnotationKind.Comment ? 1.6d : 1d);
            overlay.ToolTip = string.IsNullOrWhiteSpace(annotation.Comment)
                ? annotation.SelectedText
                : string.Concat(annotation.Comment, Environment.NewLine, Environment.NewLine, annotation.SelectedText);
        }
    }

    private void RenderSearchHighlight(Canvas canvas, PdfPageStudyData page)
    {
        if (currentSearchHitIndex < 0 || currentSearchHitIndex >= searchHits.Count)
        {
            return;
        }

        PdfTextSearchHit hit = searchHits[currentSearchHitIndex];
        if (hit.PageNumber != page.PageNumber)
        {
            return;
        }

        foreach (int index in hit.WordIndexes)
        {
            if (index < 0 || index >= page.Words.Count)
            {
                continue;
            }

            AddWordOverlay(canvas, page, page.Words[index], new SolidColorBrush(Color.FromArgb(80, 37, 99, 235)), new SolidColorBrush(Color.FromArgb(205, 37, 99, 235)), 1.5d);
        }
    }

    private void RenderTextSelection(Canvas canvas, PdfPageStudyData page)
    {
        foreach (int index in selectedWordIndexes)
        {
            if (index < 0 || index >= page.Words.Count)
            {
                continue;
            }

            AddWordOverlay(canvas, page, page.Words[index], new SolidColorBrush(Color.FromArgb(75, 14, 165, 233)), new SolidColorBrush(Color.FromArgb(205, 2, 132, 199)), 1.25d);
        }
    }
}
