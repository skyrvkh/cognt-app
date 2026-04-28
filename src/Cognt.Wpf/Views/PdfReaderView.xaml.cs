using System.IO;
using System.Windows;
using System.Windows.Controls;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Microsoft.Web.WebView2.Core;

namespace CogntApp.Views;

public partial class PdfReaderView : UserControl
{
    private bool hasCurrentDocument;

    public PdfReaderView()
    {
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
