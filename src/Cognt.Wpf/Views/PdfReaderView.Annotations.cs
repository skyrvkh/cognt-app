using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace CogntApp.Views;

public partial class PdfReaderView
{
    private void CopySelectionButton_Click(object sender, RoutedEventArgs e)
    {
        string selectedText = GetSelectedText();
        if (string.IsNullOrWhiteSpace(selectedText))
        {
            return;
        }

        try
        {
            Clipboard.SetText(selectedText);
            PdfStatusText.Text = "Selected text copied to the clipboard.";
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not copy PDF text selection to the clipboard.");
            PdfStatusText.Text = "Could not copy the selected text.";
        }
    }

    private void HighlightSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        AddAnnotation(PdfStudyAnnotationKind.Highlight, comment: null);
    }

    private void AddCommentButton_Click(object sender, RoutedEventArgs e)
    {
        string comment = CommentTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(comment))
        {
            PdfStatusText.Text = "Write a comment before adding it to the selection.";
            CommentTextBox.Focus();
            return;
        }

        if (AddAnnotation(PdfStudyAnnotationKind.Comment, comment))
        {
            CommentTextBox.Clear();
        }
    }

    private void RemoveLatestAnnotationButton_Click(object sender, RoutedEventArgs e)
    {
        PdfStudyAnnotation? latest = annotations
            .Where(annotation => annotation.PageNumber == currentPageNumber)
            .OrderByDescending(annotation => annotation.CreatedAtUtc)
            .FirstOrDefault();
        if (latest is null)
        {
            return;
        }

        annotations.Remove(latest);
        SaveAnnotations();
        RefreshCurrentPage();
        PdfStatusText.Text = "Removed the latest annotation on this page.";
    }

    private async void ExportAnnotationsButton_Click(object sender, RoutedEventArgs e)
    {
        if (exportAnnotations is null || annotations.Count == 0 || string.IsNullOrWhiteSpace(documentPath))
        {
            return;
        }

        try
        {
            ExportAnnotationsButton.IsEnabled = false;
            PdfStatusText.Text = "Creating note from annotations...";
            string relativePath = await exportAnnotations(new PdfAnnotationExport(
                documentPath,
                sourceTitle,
                annotations.OrderBy(annotation => annotation.PageNumber)
                    .ThenBy(annotation => annotation.CreatedAtUtc)
                    .ToArray())).ConfigureAwait(true);
            PdfStatusText.Text = $"Exported {annotations.Count} annotation{(annotations.Count == 1 ? string.Empty : "s")} to {relativePath}.";
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "PDF annotation export failed. SourcePath={SourcePath}", documentPath);
            PdfStatusText.Text = $"Could not export annotations: {ex.Message}";
        }
        finally
        {
            RefreshControls();
        }
    }

    private bool AddAnnotation(PdfStudyAnnotationKind kind, string? comment)
    {
        PdfStudyAnnotation? annotation = CreateAnnotationFromSelection(kind, comment);
        if (annotation is null)
        {
            PdfStatusText.Text = "Select text on the page first.";
            return false;
        }

        annotations.Add(annotation);
        SaveAnnotations();
        RefreshCurrentPage();
        PdfStatusText.Text = kind == PdfStudyAnnotationKind.Comment
            ? "Comment saved with the selected text."
            : "Highlight saved with the selected text.";
        return true;
    }

    private PdfStudyAnnotation? CreateAnnotationFromSelection(PdfStudyAnnotationKind kind, string? comment)
    {
        PdfPageStudyData? page = GetCurrentPageData();
        if (page is null || selectedWordIndexes.Count == 0)
        {
            return null;
        }

        PdfTextWord[] words = selectedWordIndexes
            .Where(index => index >= 0 && index < page.Words.Count)
            .OrderBy(index => index)
            .Select(index => page.Words[index])
            .ToArray();
        if (words.Length == 0 || page.Width <= 0 || page.Height <= 0)
        {
            return null;
        }

        double left = words.Min(word => word.Left);
        double right = words.Max(word => word.Right);
        double top = words.Max(word => word.Top);
        double bottom = words.Min(word => word.Bottom);
        double normalizedLeft = Math.Clamp(left / page.Width, 0d, 1d);
        double normalizedTop = Math.Clamp((page.Height - top) / page.Height, 0d, 1d);
        double normalizedRight = Math.Clamp(right / page.Width, normalizedLeft, 1d);
        double normalizedBottom = Math.Clamp((page.Height - bottom) / page.Height, normalizedTop, 1d);

        return new PdfStudyAnnotation(
            Guid.NewGuid().ToString("N"),
            page.PageNumber,
            string.Join(" ", words.Select(word => word.Text)),
            normalizedLeft,
            normalizedTop,
            Math.Max(0.002d, normalizedRight - normalizedLeft),
            Math.Max(0.002d, normalizedBottom - normalizedTop),
            kind,
            string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            DateTimeOffset.UtcNow);
    }

    private string GetSelectedText()
    {
        PdfPageStudyData? page = GetCurrentPageData();
        if (page is null || selectedWordIndexes.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(" ", selectedWordIndexes
            .Where(index => index >= 0 && index < page.Words.Count)
            .OrderBy(index => index)
            .Select(index => page.Words[index].Text));
    }

    private void LoadAnnotations()
    {
        if (string.IsNullOrWhiteSpace(annotationStorePath) || !File.Exists(annotationStorePath))
        {
            return;
        }

        try
        {
            PdfAnnotationStoreDocument? document = JsonSerializer.Deserialize<PdfAnnotationStoreDocument>(
