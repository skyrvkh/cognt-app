using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.ViewModels.Navigation;
using Microsoft.Extensions.Logging;

namespace CogntApp;

public partial class MainWindow
{
    private async void ViewModel_OpenPdfRequested(object? sender, OpenPdfRequestedEventArgs e)
    {
        try
        {
            await OpenPdfInBrowserAsync(e.Document);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Open PDF in browser failed. RelativePath={RelativePath}", e.Document.RelativePath);
            shellDialogs.ShowWarning("Open PDF Failed", $"The PDF could not be opened:{Environment.NewLine}{ex.Message}");
        }
    }

    private async Task OpenPdfInBrowserAsync(KnowledgePdfDocumentReadModel document)
    {
        ArgumentNullException.ThrowIfNull(document);

        string documentPath = viewModel.ResolveDocumentPath(document.RelativePath);
        ShowDockTool(BrowserAnchorable);
        await rightPaneBrowserView!.OpenPdfAsync(document.Title, documentPath);
    }
}
