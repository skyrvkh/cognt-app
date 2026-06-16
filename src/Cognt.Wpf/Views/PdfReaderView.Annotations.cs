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
