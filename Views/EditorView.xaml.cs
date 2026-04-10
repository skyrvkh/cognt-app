using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CogntApp.Components;
using CogntApp.Models.Markdown;
using CogntApp.Services;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using CreateExtractRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.CreateExtractRequestedEventArgs;
using CreateLinkedNoteRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.CreateLinkedNoteRequestedEventArgs;
using CreateLinkedNoteResult = Cognt.Wpf.EditorHost.Models.Editor.CreateLinkedNoteResult;
using MentionPreviewRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.MentionPreviewRequestedEventArgs;
using MentionSearchRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.MentionSearchRequestedEventArgs;
using OpenMentionRequestedEventArgs = Cognt.Wpf.EditorHost.Models.Editor.OpenMentionRequestedEventArgs;
using SaveClozeRequestedEventArgs = CogntApp.Models.Editor.SaveClozeRequestedEventArgs;

namespace CogntApp.Views;

public partial class EditorView : UserControl
{
    private static readonly TimeSpan EditorOperationTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan HostStartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MinimumDocumentLoadTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumDocumentLoadTimeout = TimeSpan.FromMinutes(4);

    private readonly BlockSuiteHostBridge blockSuiteBridge = new();
    private IReadOnlyList<MentionTarget> mentionTargets = [];
    private string currentContent = string.Empty;
    private string currentTitle = string.Empty;
    private string? currentSourcePath;
    private Action<string>? transientContentChanged;
    private int loadRequestId;
    private bool isShowingCardSide;
    private bool isShowingTransientEditor;

    public event EventHandler<string>? ContentChanged;
    public event EventHandler<CreateExtractRequestedEventArgs>? CreateExtractRequested;
    public event EventHandler<MentionSearchRequestedEventArgs>? MentionSearchRequested;
    public event EventHandler<MentionPreviewRequestedEventArgs>? MentionPreviewRequested;
    public event EventHandler<OpenMentionRequestedEventArgs>? OpenMentionRequested;
    public event EventHandler<CreateLinkedNoteRequestedEventArgs>? CreateLinkedNoteRequested;

    public bool IsShowingTransientEditor => isShowingTransientEditor;

    public EditorView()
    {
        InitializeComponent();
        ConfigureBlockSuiteHost();
        BindBlockSuiteBridge();
    }

    public async Task WarmUpAsync()
    {
        BlockSuiteHostView.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.Loaded);

        await WaitWithTimeoutAsync(
            blockSuiteBridge.WaitUntilReadyAsync(),
            HostStartupTimeout,
            "The BlockSuite host did not finish starting.");
    }

    public async Task LoadNoteAsync(
        string title,
        string content,
        string? path = null)
    {
        ClearTransientEditorState();
        int requestId = ++loadRequestId;
        UpdateCurrentNoteTitle(title);
        currentContent = content;
        currentSourcePath = path;

        await WarmUpAsync();

        if (requestId != loadRequestId)
        {
            ClearTransientEditorState();
            return;
        }

        await WaitWithTimeoutAsync(
            blockSuiteBridge.LoadNoteAsync(path, title, content),
            GetDocumentLoadTimeout(content),
            "The editor did not finish loading the note.");

        isShowingCardSide = false;
        SetBlockSuiteHostVisible(true);
        await FlushMentionTargetsAsync();
    }

    public Task ClearNoteAsync()
    {
        return LoadNoteAsync(string.Empty, string.Empty);
    }

    public void UpdateCurrentNoteTitle(string title)
    {
        currentTitle = title;
        NoteTitle.Text = title;
    }

    public async Task RenderCardInHostAsync(ContentControl host, string content)
    {
        ClearTransientEditorState();
        int requestId = ++loadRequestId;

        await WarmUpAsync();

        if (requestId != loadRequestId)
        {
            return;
        }

        SetBlockSuiteHostVisible(false);

        try
        {
            await WaitWithTimeoutAsync(
                blockSuiteBridge.BeginCardRenderAsync(),
                EditorOperationTimeout,
                "The shared BlockSuite host did not enter review rendering mode.");

            if (requestId != loadRequestId)
            {
                return;
            }

            AttachBlockSuiteHostTo(host);

            await WaitWithTimeoutAsync(
                blockSuiteBridge.LoadCardAsync(content),
                GetDocumentLoadTimeout(content),
                "The shared BlockSuite host did not finish loading the review card.");

            if (requestId == loadRequestId)
            {
                isShowingCardSide = true;
            }
        }
        finally
        {
            if (requestId == loadRequestId)
            {
                SetBlockSuiteHostVisible(true);
            }
        }
    }

    public async Task EditTransientNoteInHostAsync(
        ContentControl host,
        string title,
        string content,
        Action<string> contentChanged)
    {
        int requestId = ++loadRequestId;

        await WarmUpAsync();

        if (requestId != loadRequestId)
        {
            return;
        }

        SetBlockSuiteHostVisible(false);
        AttachBlockSuiteHostTo(host);
        PrepareInteractiveBlockSuiteHost(host);
        transientContentChanged = contentChanged;

        try
        {
            await WaitWithTimeoutAsync(
                blockSuiteBridge.ActivateEditorAsync(),
                EditorOperationTimeout,
                "The shared BlockSuite editor did not leave read-only mode.");

            await WaitWithTimeoutAsync(
                blockSuiteBridge.LoadNoteAsync("card-workbench.json", title, content),
                GetDocumentLoadTimeout(content),
                "The shared BlockSuite editor did not finish loading the card text.");

            if (requestId != loadRequestId)
            {
                ClearTransientEditorState();
                return;
            }

            SetBlockSuiteHostVisible(true);
            await FocusBlockSuiteHostAsync(host);

            await WaitWithTimeoutAsync(
                blockSuiteBridge.FocusEditorAsync(),
                EditorOperationTimeout,
                "The shared BlockSuite editor did not accept focus.");

            if (requestId == loadRequestId)
            {
                isShowingCardSide = false;
                isShowingTransientEditor = true;
            }
        }
        catch
        {
            if (requestId == loadRequestId)
            {
                ClearTransientEditorState();
                AttachBlockSuiteHostToEditor();
            }

            throw;
        }
        finally
        {
            if (requestId == loadRequestId)
            {
                SetBlockSuiteHostVisible(true);
            }
        }
    }

    public async Task RestoreNoteAfterTransientEditorAsync()
    {
        bool shouldRestore = isShowingTransientEditor;
        if (!shouldRestore)
        {
            return;
        }

        if (shouldRestore)
        {
            SetBlockSuiteHostVisible(false);
        }

        ClearTransientEditorState();
        AttachBlockSuiteHostToEditor();

        try
        {
            await WaitWithTimeoutAsync(
                blockSuiteBridge.LoadNoteAsync(currentSourcePath, currentTitle, currentContent),
                GetDocumentLoadTimeout(currentContent),
                "The editor did not restore the current note after card editing.");
            await FlushMentionTargetsAsync();
        }
        finally
        {
            SetBlockSuiteHostVisible(true);
        }
    }

    public async Task RestoreNoteAfterCardPreviewAsync()
    {
        bool shouldHideStaleCard = isShowingCardSide;
        if (shouldHideStaleCard)
        {
            SetBlockSuiteHostVisible(false);
        }

        AttachBlockSuiteHostToEditor();

        if (!isShowingCardSide)
        {
            SetBlockSuiteHostVisible(true);
            return;
        }

        try
        {
            await LoadNoteAsync(currentTitle, currentContent, currentSourcePath);
        }
        finally
        {
            SetBlockSuiteHostVisible(true);
        }
    }

    public string GetContent()
    {
        return currentContent;
    }

    private void AttachBlockSuiteHostTo(ContentControl host)
    {
        RemoveBlockSuiteHostFromParent();
        host.Content = BlockSuiteHostView;
        PrepareInteractiveBlockSuiteHost(host);
    }

    public void AttachBlockSuiteHostToEditor()
    {
        if (ReferenceEquals(EditorWebViewFrame.Child, BlockSuiteHostView))
        {
            return;
        }

        RemoveBlockSuiteHostFromParent();
        EditorWebViewFrame.Child = BlockSuiteHostView;
    }

    public void DetachBlockSuiteHost()
    {
        SetBlockSuiteHostVisible(false);
        RemoveBlockSuiteHostFromParent();
    }

    public void PostExtractResult(string? path, bool ok, string? createdPath = null, string? error = null)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.ShowExtractResultAsync(ok, createdPath, error),
            "show extract result");
    }

    public void PostMentionTargets(IEnumerable<MentionTarget> targets)
    {
        mentionTargets = targets.ToList();
        RunBlockSuiteCommand(FlushMentionTargetsAsync(), "flush mention targets");
    }

    public void PostMentionPreviewResult(
        string requestId,
        string relativePath,
        string mode,
        bool ok,
        string? title = null,
        string? documentContent = null,
        string? error = null)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.HandleMentionPreviewResultAsync(
                requestId,
                relativePath,
                mode,
                ok,
                title,
                documentContent,
                error),
            "post mention preview result");
    }

    public void PostMentionOpenResult(string relativePath, bool ok, string? error = null)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.HandleMentionOpenResultAsync(ok, error),
            "post mention open result");
    }

    public void PostSaveResult(string? path, bool ok, string? error = null)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.ShowSaveResultAsync(ok, error),
            "show save result");
    }

    private void ConfigureBlockSuiteHost()
    {
        BlockSuiteHostView.Services = BlazorHybridServices.Services;
        BlockSuiteHostView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(BlockSuiteHost),
            Parameters = new Dictionary<string, object?>
            {
                [nameof(BlockSuiteHost.Bridge)] = blockSuiteBridge,
            },
        });
    }

    private void BindBlockSuiteBridge()
    {
        blockSuiteBridge.ContentChanged += BlockSuiteBridge_ContentChanged;
        blockSuiteBridge.CreateExtractRequested += BlockSuiteBridge_CreateExtractRequested;
        blockSuiteBridge.SaveClozeRequested += BlockSuiteBridge_SaveClozeRequested;
        blockSuiteBridge.MentionSearchRequested += BlockSuiteBridge_MentionSearchRequested;
        blockSuiteBridge.MentionPreviewRequested += BlockSuiteBridge_MentionPreviewRequested;
        blockSuiteBridge.OpenMentionRequested += BlockSuiteBridge_OpenMentionRequested;
        blockSuiteBridge.CreateLinkedNoteRequested += BlockSuiteBridge_CreateLinkedNoteRequested;
        blockSuiteBridge.DiagnosticReceived += BlockSuiteBridge_DiagnosticReceived;
    }

    private void BlockSuiteBridge_ContentChanged(object? sender, string content)
    {
        try
        {
            if (transientContentChanged is not null)
            {
                transientContentChanged(content);
                return;
            }

            currentContent = content;
            ContentChanged?.Invoke(this, currentContent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BlockSuite content change handling failed: {ex}");
            PostSaveResult(currentSourcePath, ok: false, error: ex.Message);
        }
    }

    private void BlockSuiteBridge_CreateExtractRequested(object? sender, CreateExtractRequestedEventArgs e)
    {
        EventHandler<CreateExtractRequestedEventArgs>? handler = CreateExtractRequested;

        if (handler is null)
        {
            PostExtractResult(e.SourcePath, ok: false, error: "No extract handler is registered.");
            return;
        }

        try
        {
            handler.Invoke(this, e);
        }
        catch (Exception ex)
        {
            PostExtractResult(e.SourcePath, ok: false, error: ex.Message);
        }
    }

    private void BlockSuiteBridge_SaveClozeRequested(object? sender, SaveClozeRequestedEventArgs e)
    {
        if (!IsCurrentSourcePath(e.SourcePath))
        {
            PostSaveResult(e.SourcePath, ok: false, error: "The cloze source is not the current note file.");
            return;
        }

        try
        {
            currentContent = e.Content;
            ContentChanged?.Invoke(this, currentContent);
            PostSaveResult(e.SourcePath, ok: true);
        }
        catch (Exception ex)
        {
            PostSaveResult(e.SourcePath, ok: false, error: ex.Message);
        }
    }

    private void BlockSuiteBridge_MentionSearchRequested(object? sender, MentionSearchRequestedEventArgs e)
    {
        if (!IsCurrentSourcePath(e.SourcePath))
        {
            e.Complete([]);
            return;
        }

        EventHandler<MentionSearchRequestedEventArgs>? handler = MentionSearchRequested;
        if (handler is null)
        {
            e.Complete([]);
            return;
        }

        try
        {
            handler.Invoke(this, e);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BlockSuite mention search failed: {ex}");
            e.Complete([]);
        }
    }

    private void BlockSuiteBridge_MentionPreviewRequested(object? sender, MentionPreviewRequestedEventArgs e)
    {
        if (!IsCurrentSourcePath(e.SourcePath))
        {
            PostMentionPreviewResult(
                e.RequestId,
                e.RelativePath,
                e.Mode,
                ok: false,
                error: "The mention preview source is not the current note file.");
            return;
        }

        EventHandler<MentionPreviewRequestedEventArgs>? handler = MentionPreviewRequested;
        if (handler is null)
        {
            PostMentionPreviewResult(
                e.RequestId,
                e.RelativePath,
                e.Mode,
                ok: false,
                error: "No mention preview handler is registered.");
            return;
        }

        try
        {
            handler.Invoke(this, e);
        }
        catch (Exception ex)
        {
            PostMentionPreviewResult(e.RequestId, e.RelativePath, e.Mode, ok: false, error: ex.Message);
        }
    }

    private void BlockSuiteBridge_OpenMentionRequested(object? sender, OpenMentionRequestedEventArgs e)
    {
        if (!IsCurrentSourcePath(e.SourcePath))
        {
            PostMentionOpenResult(e.RelativePath, ok: false, error: "The mention source is not the current note file.");
            return;
        }

        EventHandler<OpenMentionRequestedEventArgs>? handler = OpenMentionRequested;
        if (handler is null)
        {
            PostMentionOpenResult(e.RelativePath, ok: false, error: "No mention open handler is registered.");
            return;
        }

        try
        {
            currentContent = e.Content;
            handler.Invoke(this, e);
        }
        catch (Exception ex)
        {
            PostMentionOpenResult(e.RelativePath, ok: false, error: ex.Message);
        }
    }

    private void BlockSuiteBridge_CreateLinkedNoteRequested(object? sender, CreateLinkedNoteRequestedEventArgs e)
    {
        if (!IsCurrentSourcePath(e.SourcePath))
        {
            e.Complete(CreateLinkedNoteResult.Fail("The linked note source is not the current note file."));
            return;
        }

        EventHandler<CreateLinkedNoteRequestedEventArgs>? handler = CreateLinkedNoteRequested;
        if (handler is null)
        {
            e.Complete(CreateLinkedNoteResult.Fail("No linked note handler is registered."));
            return;
        }

        try
        {
            handler.Invoke(this, e);
        }
        catch (Exception ex)
        {
            e.Complete(CreateLinkedNoteResult.Fail(ex.Message));
        }
    }

    private static void BlockSuiteBridge_DiagnosticReceived(object? sender, string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            System.Diagnostics.Debug.WriteLine($"BlockSuite host: {message}");
        }
    }

    private async Task FlushMentionTargetsAsync()
    {
        if (mentionTargets.Count == 0)
        {
            return;
        }

        try
        {
            await blockSuiteBridge.SetMentionTargetsAsync(mentionTargets);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BlockSuite mention target update failed: {ex}");
        }
    }

    private void RemoveBlockSuiteHostFromParent()
    {
        switch (BlockSuiteHostView.Parent)
        {
            case Border border when ReferenceEquals(border.Child, BlockSuiteHostView):
                border.Child = null;
                break;

            case ContentControl contentControl when ReferenceEquals(contentControl.Content, BlockSuiteHostView):
                contentControl.Content = null;
                break;

            case Panel panel:
                panel.Children.Remove(BlockSuiteHostView);
                break;
        }
    }

    private void SetBlockSuiteHostVisible(bool isVisible)
    {
        BlockSuiteHostView.Visibility = Visibility.Visible;
        BlockSuiteHostView.IsEnabled = true;
        BlockSuiteHostView.Focusable = true;
        BlockSuiteHostView.Opacity = isVisible ? 1 : 0;
        BlockSuiteHostView.IsHitTestVisible = isVisible;
    }

    private void ClearTransientEditorState()
    {
        transientContentChanged = null;
        isShowingTransientEditor = false;
    }

    private bool IsCurrentSourcePath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && !string.IsNullOrWhiteSpace(currentSourcePath)
            && string.Equals(Path.GetFullPath(path), Path.GetFullPath(currentSourcePath), StringComparison.OrdinalIgnoreCase);
    }

    private void PrepareInteractiveBlockSuiteHost(ContentControl host)
    {
        host.Focusable = true;
        host.IsEnabled = true;
        host.IsHitTestVisible = true;
        BlockSuiteHostView.Visibility = Visibility.Visible;
        BlockSuiteHostView.IsEnabled = true;
        BlockSuiteHostView.Focusable = true;
    }

    private async Task FocusBlockSuiteHostAsync(ContentControl host)
    {
        PrepareInteractiveBlockSuiteHost(host);
        host.UpdateLayout();
        BlockSuiteHostView.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.Input);
        host.Focus();
        BlockSuiteHostView.Focus();
        Keyboard.Focus(BlockSuiteHostView);
        FocusManager.SetFocusedElement(host, BlockSuiteHostView);
    }

    private static async Task WaitWithTimeoutAsync(Task task, TimeSpan timeout, string timeoutMessage)
    {
        Task completedTask = await Task.WhenAny(task, Task.Delay(timeout));

        if (completedTask != task)
        {
            RunBlockSuiteCommand(task, "timed-out operation");
            throw new TimeoutException(timeoutMessage);
        }

        await task;
    }

    private static async Task<T> WaitWithTimeoutAsync<T>(Task<T> task, TimeSpan timeout, string timeoutMessage)
    {
        Task completedTask = await Task.WhenAny(task, Task.Delay(timeout));

        if (completedTask != task)
        {
            RunBlockSuiteCommand(task, "timed-out operation");
            throw new TimeoutException(timeoutMessage);
        }

        return await task;
    }

    private static TimeSpan GetDocumentLoadTimeout(string? content)
    {
        int contentLength = content?.Length ?? 0;
        int totalSeconds = 30 + (int)Math.Ceiling(contentLength / 10_000d);

        return TimeSpan.FromSeconds(Math.Clamp(
            totalSeconds,
            (int)MinimumDocumentLoadTimeout.TotalSeconds,
            (int)MaximumDocumentLoadTimeout.TotalSeconds));
    }

    private static void RunBlockSuiteCommand(Task task, string operation)
    {
        _ = ObserveBlockSuiteCommandAsync(task, operation);
    }

    private static async Task ObserveBlockSuiteCommandAsync(Task task, string operation)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BlockSuite {operation} failed: {ex}");
        }
    }
}
