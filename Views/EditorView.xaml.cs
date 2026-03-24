using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace CogntApp.Views;

public partial class EditorView : UserControl
{
    private const string EditorHostName = "cognt-editor.local";
    private const string EditorOutputDirectoryName = "EditorWeb";
    private const string LoadMessageType = "load";
    private const string ReadyMessageType = "ready";
    private const string ContentChangedMessageType = "contentChanged";
    private const string DiagnosticMessageType = "diagnostic";

    private string currentContent = string.Empty;
    private string pendingTitle = string.Empty;
    private string pendingContent = string.Empty;
    private bool editorReady;

    public event EventHandler<string>? ContentChanged;

    public EditorView()
    {
        InitializeComponent();
        _ = InitializeEditorAsync();
    }

    public void ShowNote(string title, string content)
    {
        NoteTitle.Text = title;
        currentContent = content;
        pendingTitle = title;
        pendingContent = content;

        if (editorReady)
        {
            SendLoadMessage();
        }
    }

    public string GetContent()
    {
        return currentContent;
    }

    private async Task InitializeEditorAsync()
    {
        string editorFolder = Path.Combine(AppContext.BaseDirectory, EditorOutputDirectoryName);

        if (!EditorBundleExists(editorFolder))
        {
            ShowMissingEditorBundleMessage(editorFolder);
            return;
        }

        CoreWebView2Environment environment = await CreateEditorEnvironmentAsync();
        await BlockSuiteEditor.EnsureCoreWebView2Async(environment);

        CoreWebView2 webView = BlockSuiteEditor.CoreWebView2;
        await InstallDiagnosticsAsync(webView);
        webView.WebMessageReceived += BlockSuiteEditor_WebMessageReceived;
        webView.SetVirtualHostNameToFolderMapping(EditorHostName, editorFolder, CoreWebView2HostResourceAccessKind.Allow);
        BlockSuiteEditor.Source = new Uri($"https://{EditorHostName}/index.html");
    }

    private void BlockSuiteEditor_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using JsonDocument message = JsonDocument.Parse(e.WebMessageAsJson);
        JsonElement root = message.RootElement;

        if (!root.TryGetProperty("type", out JsonElement typeElement))
        {
            return;
        }

        string? type = typeElement.GetString();

        if (type == ReadyMessageType)
        {
            editorReady = true;
            SendLoadMessage();
            return;
        }

        if (type == ContentChangedMessageType && root.TryGetProperty("content", out JsonElement contentElement))
        {
            currentContent = contentElement.GetString() ?? string.Empty;
            ContentChanged?.Invoke(this, currentContent);
            return;
        }

        if (type == DiagnosticMessageType && root.TryGetProperty("message", out JsonElement diagnosticElement))
        {
            System.Diagnostics.Debug.WriteLine($"BlockSuite editor: {diagnosticElement.GetString()}");
        }
    }

    private void SendLoadMessage()
    {
        if (BlockSuiteEditor.CoreWebView2 is null)
        {
            return;
        }

        string payload = JsonSerializer.Serialize(new
        {
            type = LoadMessageType,
            title = pendingTitle,
            content = pendingContent,
        });

        BlockSuiteEditor.CoreWebView2.PostWebMessageAsJson(payload);
    }

    private static bool EditorBundleExists(string editorFolder)
    {
        return File.Exists(Path.Combine(editorFolder, "index.html"));
    }

    private static async Task<CoreWebView2Environment> CreateEditorEnvironmentAsync()
    {
        string userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CogntApp",
            "WebView2");

        return await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
    }

    private static async Task InstallDiagnosticsAsync(CoreWebView2 webView)
    {
        const string script =
            "window.addEventListener('error', event => chrome.webview.postMessage({ type: 'diagnostic', level: 'error', message: event.message }));" +
            "window.addEventListener('unhandledrejection', event => chrome.webview.postMessage({ type: 'diagnostic', level: 'error', message: String(event.reason) }));";

        await webView.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    private static void ShowMissingEditorBundleMessage(string editorFolder)
    {
        MessageBox.Show(
            $"The BlockSuite editor bundle was not found:{Environment.NewLine}{editorFolder}{Environment.NewLine}{Environment.NewLine}Run npm install and npm run build in EditorWeb, then build the app again.",
            "Editor Bundle Missing",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
