using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.ViewModels.Navigation;
using CogntApp.Views;

namespace CogntApp;

public partial class MainWindow
{
    private async void ViewModel_OpenPdfRequested(object? sender, OpenPdfRequestedEventArgs e)
    {
        await OpenPdfInRightPaneAsync(e.Document);
    }

    private void RightViewerPdfButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPdfRightPane();
    }

    private void RightViewerBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        BrowserPaneView browserView = ShowBrowserRightPane();
        browserView.FocusAddressBox();
    }

    private void RightViewerCloseButton_Click(object sender, RoutedEventArgs e)
    {
        CollapseRightPane();
    }

    private void RightViewerSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (RightViewerPane.Visibility != Visibility.Visible)
        {
            return;
        }

        double totalAvailableWidth = MainContentSplitRoot.ActualWidth - RightViewerSplitter.ActualWidth;
        if (totalAvailableWidth <= 0)
        {
            return;
        }

        double nextRightPaneWidth = Math.Clamp(
            RightViewerColumn.ActualWidth - e.HorizontalChange,
            RightViewerMinWidth,
            totalAvailableWidth);
        double nextPrimaryWidth = Math.Max(0, totalAvailableWidth - nextRightPaneWidth);

        PrimaryContentColumn.Width = new GridLength(nextPrimaryWidth, GridUnitType.Pixel);
        RightViewerColumn.Width = new GridLength(nextRightPaneWidth, GridUnitType.Pixel);
        e.Handled = true;
    }

    private void RightViewerSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (RightViewerColumn.ActualWidth >= RightViewerMinWidth)
        {
            lastRightViewerWidth = new GridLength(Math.Max(
                RightViewerColumn.ActualWidth,
                RightViewerMinWidth));
        }
    }

    private void RightViewerPane_DragEnter(object sender, DragEventArgs e)
    {
        HandleRightPanePdfDragOver(e);
    }

    private void RightViewerPane_DragOver(object sender, DragEventArgs e)
    {
        HandleRightPanePdfDragOver(e);
    }

    private void RightViewerPane_DragLeave(object sender, DragEventArgs e)
    {
        RightViewerDropOverlay.Visibility = Visibility.Collapsed;
    }

    private async void RightViewerPane_Drop(object sender, DragEventArgs e)
    {
        RightViewerDropOverlay.Visibility = Visibility.Collapsed;
        if (!TryGetPdfDropPayload(e, out KnowledgePdfDocumentReadModel? document, out string? documentPath)
            || string.IsNullOrWhiteSpace(documentPath))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
        ShowPdfRightPane();

        PdfReaderView pdfView = GetOrCreatePdfRightPane();
        if (document is not null)
        {
            await pdfView.LoadPdfAsync(document, documentPath);
        }
        else
        {
            await pdfView.LoadPdfFileAsync(documentPath);
        }
    }

    private async Task OpenPdfInRightPaneAsync(KnowledgePdfDocumentReadModel document)
    {
        ArgumentNullException.ThrowIfNull(document);

        ShowPdfRightPane();
        string documentPath = viewModel.ResolveDocumentPath(document.RelativePath);
        await GetOrCreatePdfRightPane().LoadPdfAsync(document, documentPath);
    }

    private PdfReaderView ShowPdfRightPane()
    {
        PdfReaderView pdfView = GetOrCreatePdfRightPane();
        ShowRightPaneContent("PDF Viewer", pdfView);
        return pdfView;
    }

    private BrowserPaneView ShowBrowserRightPane()
    {
        rightPaneBrowserView ??= new BrowserPaneView();
        ShowRightPaneContent("Browser", rightPaneBrowserView);
        return rightPaneBrowserView;
    }

    private PdfReaderView GetOrCreatePdfRightPane()
    {
        rightPanePdfView ??= new PdfReaderView();
        return rightPanePdfView;
    }

    private void ShowRightPaneContent(string title, UserControl content)
    {
        RightViewerTitleText.Text = title;
        RightViewerContentHost.Content = content;

        if (RightViewerColumn.Width.Value < RightViewerMinWidth)
        {
            RightViewerColumn.Width = lastRightViewerWidth.Value >= RightViewerMinWidth
                ? lastRightViewerWidth
                : new GridLength(RightViewerDefaultWidth);
        }

        RightViewerColumn.MinWidth = RightViewerMinWidth;
        RightViewerSplitterColumn.Width = new GridLength(6);
        RightViewerPane.Visibility = Visibility.Visible;
        RightViewerSplitter.Visibility = Visibility.Visible;
    }

    private void CollapseRightPane()
    {
        if (RightViewerPane.Visibility == Visibility.Visible && RightViewerColumn.ActualWidth >= RightViewerMinWidth)
        {
            lastRightViewerWidth = new GridLength(Math.Max(
                RightViewerColumn.ActualWidth,
                RightViewerMinWidth));
        }

        RightViewerDropOverlay.Visibility = Visibility.Collapsed;
        RightViewerPane.Visibility = Visibility.Collapsed;
        RightViewerSplitter.Visibility = Visibility.Collapsed;
        RightViewerColumn.MinWidth = 0;
        RightViewerColumn.Width = new GridLength(0);
        RightViewerSplitterColumn.Width = new GridLength(0);
    }

    private void HandleRightPanePdfDragOver(DragEventArgs e)
    {
        bool acceptsPdf = TryGetPdfDropPayload(e, out _, out _);
        e.Effects = acceptsPdf ? DragDropEffects.Copy : DragDropEffects.None;
        RightViewerDropOverlay.Visibility = acceptsPdf ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private bool TryGetPdfDropPayload(
        DragEventArgs e,
        out KnowledgePdfDocumentReadModel? document,
        out string? documentPath)
    {
        document = null;
        documentPath = null;

        if (e.Data.GetDataPresent(DataFormats.FileDrop)
            && e.Data.GetData(DataFormats.FileDrop) is string[] droppedPaths)
        {
            string? pdfPath = droppedPaths.FirstOrDefault(IsExistingPdfPath);
            if (!string.IsNullOrWhiteSpace(pdfPath))
            {
                documentPath = pdfPath;
                return true;
            }
        }

        if (e.Data.GetDataPresent(NoteTreeDragDataFormat)
            && e.Data.GetData(NoteTreeDragDataFormat) is long itemId
            && viewModel.TryGetPdfDocumentByTreeItemId(itemId, out document)
            && document is not null)
        {
            documentPath = viewModel.ResolveDocumentPath(document.RelativePath);
            return File.Exists(documentPath);
        }

        return false;
    }

    private static bool IsExistingPdfPath(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && File.Exists(path)
            && string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
    }
}
