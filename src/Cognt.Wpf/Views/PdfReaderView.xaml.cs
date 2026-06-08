using System.IO;
using System.Windows;
using System.Windows.Controls;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace CogntApp.Views;

public partial class PdfReaderView : UserControl
{
    private bool hasCurrentDocument;
    private readonly ILogger<PdfReaderView>? logger;

    public PdfReaderView(ILogger<PdfReaderView>? logger = null)
    {
        this.logger = logger;
        InitializeComponent();
        ShowEmptyState();
    }

    internal async Task LoadPdfAsync(KnowledgePdfDocumentReadModel? document, string? documentPath)
    {
        hasCurrentDocument = document is not null;

        if (document is null || string.IsNullOrWhiteSpace(documentPath))
        {
            ShowEmptyState();
            return;
        }

        if (!File.Exists(documentPath))
        {
            logger?.LogWarning(
                "PDF reader load skipped because file is missing. RelativePath={RelativePath} DocumentPath={DocumentPath}",
                document.RelativePath,
                documentPath);
            ShowState(
                "Missing PDF file",
                "The selected PDF file is missing from workspace storage.");
            return;
        }

        ShowState("Loading PDF", "Preparing the reader...");

        try
        {
            await PdfWebView.EnsureCoreWebView2Async();
            PdfWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            PdfWebView.CoreWebView2.Settings.IsStatusBarEnabled = true;
            PdfWebView.CoreWebView2.Navigate(new Uri(documentPath).AbsoluteUri);
            PdfWebView.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "PDF reader load failed. RelativePath={RelativePath} DocumentPath={DocumentPath}", document.RelativePath, documentPath);
            ShowState("Failed to load PDF", ex.Message);
        }
    }

    internal Task LoadPdfFileAsync(string documentPath)
    {
        KnowledgePdfDocumentReadModel document = new(
            documentPath,
            Path.GetFileNameWithoutExtension(documentPath));
        return LoadPdfAsync(document, documentPath);
    }

    private void ShowEmptyState()
    {
        hasCurrentDocument = false;
        ShowState("No PDF selected", "Select or drop a PDF to read it beside the current note.");
    }

    private void PdfWebView_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!hasCurrentDocument)
        {
            return;
        }

        if (e.IsSuccess)
        {
            StatePanel.Visibility = Visibility.Collapsed;
            PdfWebView.Visibility = Visibility.Visible;
            return;
        }

        logger?.LogWarning("PDF reader navigation completed with error. Uri={Uri} WebErrorStatus={WebErrorStatus}", PdfWebView.Source, e.WebErrorStatus);
        ShowState("Failed to load PDF", $"WebView2 could not open the PDF. Error: {e.WebErrorStatus}");
    }

    private void ShowState(string title, string message)
    {
        PdfWebView.Visibility = Visibility.Collapsed;
        StatePanel.Visibility = Visibility.Visible;
        StateTitleText.Text = title;
        StateMessageText.Text = message;
    }
}
