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
    private static readonly TimeSpan EditorOperationTimeout = TimeSpan.FromSeconds(8);

    private readonly TaskCompletionSource editorReadyCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource? currentLoadCompletion;
    private Task? initializeEditorTask;
    private string currentContent = string.Empty;
    private string? currentSourcePath;
    private int loadRequestId;
    private int lastSentLoadRequestId;

    public event EventHandler<string>? ContentChanged;
    public event EventHandler<CreateExtractRequestedEventArgs>? CreateExtractRequested;

    public EditorView()
    {
        InitializeComponent();
        BlockSuiteEditor.DefaultBackgroundColor = System.Drawing.Color.White;
    }

    public async Task WarmUpAsync()
    {
        await EnsureEditorLoadingAsync();
        await WaitWithTimeoutAsync(
            editorReadyCompletion.Task,
            EditorOperationTimeout,
            "The editor did not finish starting.");
    }

    public async Task LoadNoteAsync(string title, string content, string? path = null)
    {
        int requestId = ++loadRequestId;
        NoteTitle.Text = title;
        currentContent = content;
        currentSourcePath = path;

        await WarmUpAsync();

        if (requestId != loadRequestId)
        {
            return;
        }

        currentLoadCompletion?.TrySetResult();
        TaskCompletionSource loadCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        currentLoadCompletion = loadCompletion;
        SendLoadMessage(requestId, path, title, content);
        await WaitWithTimeoutAsync(
            loadCompletion.Task,
            EditorOperationTimeout,
            "The editor did not finish loading the note.");
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
            editorReadyCompletion.TrySetException(new FileNotFoundException("The BlockSuite editor bundle was not found.", editorFolder));
            return;
        }

        try
        {
            CoreWebView2Environment environment = await CreateEditorEnvironmentAsync();
            await BlockSuiteEditor.EnsureCoreWebView2Async(environment);

            CoreWebView2 webView = BlockSuiteEditor.CoreWebView2;
            await InstallDiagnosticsAsync(webView);
            webView.WebMessageReceived += BlockSuiteEditor_WebMessageReceived;
            webView.SetVirtualHostNameToFolderMapping(EditorHostName, editorFolder, CoreWebView2HostResourceAccessKind.Allow);
            BlockSuiteEditor.Source = new Uri($"https://{EditorHostName}/index.html");
        }
        catch (Exception ex)
        {
            editorReadyCompletion.TrySetException(ex);
            System.Diagnostics.Debug.WriteLine($"BlockSuite editor failed to start: {ex}");
        }
    }

    private void BlockSuiteEditor_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using JsonDocument message = JsonDocument.Parse(e.WebMessageAsJson);
        JsonElement root = message.RootElement;

        if (!root.TryGetProperty("type", out JsonElement typeElement))
        {
            return;
        }

        switch (typeElement.GetString())
        {
            case EditorWebMessageTypes.Ready:
                editorReadyCompletion.TrySetResult();
                return;

            case EditorWebMessageTypes.Loaded:
                CompleteLoadMessage(root);
                return;

            case EditorWebMessageTypes.ContentChanged:
                HandleContentChangedMessage(root);
                return;

            case EditorWebMessageTypes.CreateExtract:
                HandleCreateExtractMessage(root);
                return;

            case EditorWebMessageTypes.SaveCloze:
                HandleSaveClozeMessage(root);
                return;

            case EditorWebMessageTypes.Diagnostic:
                WriteDiagnosticMessage(root);
                return;
        }
    }

    public void PostExtractResult(string? path, bool ok, string? createdPath = null, string? error = null)
    {
        PostMessage(new
        {
            type = EditorWebMessageTypes.ExtractResult,
            path,
            ok,
            createdPath,
            error,
        });
    }

    public void PostSaveResult(string? path, bool ok, string? error = null)
    {
        PostMessage(new
        {
            type = EditorWebMessageTypes.SaveResult,
            path,
            ok,
            error,
        });
    }

    private void HandleCreateExtractMessage(JsonElement root)
    {
        string? sourcePath = GetOptionalString(root, "sourcePath");
        string selectedMarkdown = GetOptionalString(root, "selectedMarkdown") ?? string.Empty;
        string selectedPlainText = GetOptionalString(root, "selectedPlainText") ?? string.Empty;
        EventHandler<CreateExtractRequestedEventArgs>? handler = CreateExtractRequested;

        if (handler is null)
        {
            PostExtractResult(sourcePath, ok: false, error: "No extract handler is registered.");
            return;
        }

        try
        {
            handler.Invoke(this, new CreateExtractRequestedEventArgs(
                sourcePath ?? string.Empty,
                selectedMarkdown,
                selectedPlainText));
        }
        catch (Exception ex)
        {
            PostExtractResult(sourcePath, ok: false, error: ex.Message);
        }
    }

    private void CompleteLoadMessage(JsonElement root)
    {
        if (TryGetLoadId(root, out int loadedRequestId) && loadedRequestId != lastSentLoadRequestId)
        {
            return;
        }

        currentLoadCompletion?.TrySetResult();
        currentLoadCompletion = null;
    }

    private void HandleContentChangedMessage(JsonElement root)
    {
        if (!root.TryGetProperty("content", out JsonElement contentElement))
        {
            return;
        }

        currentContent = contentElement.GetString() ?? string.Empty;
        ContentChanged?.Invoke(this, currentContent);
    }

    private void HandleSaveClozeMessage(JsonElement root)
    {
        string? sourcePath = GetOptionalString(root, "sourcePath");
        string markdown = GetOptionalString(root, "markdown") ?? currentContent;

        if (!IsCurrentSourcePath(sourcePath))
        {
            PostSaveResult(sourcePath, ok: false, error: "The cloze source is not the current Markdown file.");
            return;
        }

        try
        {
            currentContent = markdown;
            ContentChanged?.Invoke(this, currentContent);
            PostSaveResult(sourcePath, ok: true);
        }
        catch (Exception ex)
        {
            PostSaveResult(sourcePath, ok: false, error: ex.Message);
        }
    }

    private static void WriteDiagnosticMessage(JsonElement root)
    {
        if (root.TryGetProperty("message", out JsonElement diagnosticElement))
        {
            System.Diagnostics.Debug.WriteLine($"BlockSuite editor: {diagnosticElement.GetString()}");
        }
    }

    private void SendLoadMessage(int requestId, string? path, string title, string content)
    {
        if (BlockSuiteEditor.CoreWebView2 is null)
        {
            return;
        }

        lastSentLoadRequestId = requestId;

        string payload = JsonSerializer.Serialize(new
        {
            type = EditorWebMessageTypes.Load,
            loadId = requestId,
            path,
            title,
            content,
        });

        BlockSuiteEditor.CoreWebView2.PostWebMessageAsJson(payload);
    }

    private void PostMessage(object message)
    {
        if (BlockSuiteEditor.CoreWebView2 is null)
        {
            return;
        }

        BlockSuiteEditor.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message));
    }

    private Task EnsureEditorLoadingAsync()
    {
        return initializeEditorTask ??= InitializeEditorAsync();
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

    private static bool TryGetLoadId(JsonElement root, out int loadId)
    {
        loadId = 0;

        return root.TryGetProperty("loadId", out JsonElement loadIdElement)
            && loadIdElement.TryGetInt32(out loadId);
    }

    private bool IsCurrentSourcePath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && !string.IsNullOrWhiteSpace(currentSourcePath)
            && string.Equals(Path.GetFullPath(path), Path.GetFullPath(currentSourcePath), StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out JsonElement property)
            ? property.GetString()
            : null;
    }

    private static async Task WaitWithTimeoutAsync(Task task, TimeSpan timeout, string timeoutMessage)
    {
        Task completedTask = await Task.WhenAny(task, Task.Delay(timeout));

        if (completedTask != task)
        {
            throw new TimeoutException(timeoutMessage);
        }

        await task;
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
