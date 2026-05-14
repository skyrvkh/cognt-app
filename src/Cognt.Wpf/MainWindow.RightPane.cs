using System.IO;
using System.Windows;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.ViewModels.Navigation;
using CogntApp.Views;

namespace CogntApp;

public partial class MainWindow
{
    private async void ViewModel_OpenPdfRequested(object? sender, OpenPdfRequestedEventArgs e)
    {
        await OpenPdfInToolWindowAsync(e.Document);
    }

    private async Task OpenPdfInToolWindowAsync(KnowledgePdfDocumentReadModel document)
    {
        ArgumentNullException.ThrowIfNull(document);

        PdfReaderView pdfView = ShowPdfToolWindow();
        string documentPath = viewModel.ResolveDocumentPath(document.RelativePath);
        await pdfView.LoadPdfAsync(document, documentPath);
    }

    private PdfReaderView ShowPdfToolWindow()
    {
        PdfReaderView pdfView = GetOrCreatePdfToolView();
        PdfToolHost.Content = pdfView;
        ShowDockTool(PdfAnchorable);
        return pdfView;
    }

    private PdfReaderView GetOrCreatePdfToolView()
    {
        rightPanePdfView ??= new PdfReaderView();
        return rightPanePdfView;
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
