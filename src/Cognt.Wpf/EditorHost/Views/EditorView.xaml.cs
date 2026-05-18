using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Abstractions;
using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Services;

namespace Cognt.Wpf.EditorHost.Views;

public partial class EditorView : UserControl
{
    private enum EditorHostState
    {
        Unloaded,
        Loading,
        Ready,
        Suspended,
        Faulted,
    }

    private readonly BlockSuiteHostBridge blockSuiteBridge = new();
    private readonly IServiceProvider serviceProvider;
    private IReadOnlyList<KnowledgeMentionTargetReadModel> mentionTargets = [];
    private string currentContent = string.Empty;
    private string currentTitle = string.Empty;
    private string? currentSourcePath;
    private string? currentCollectionRoot;
    private Action<string>? transientContentChanged;
    private int loadRequestId;
    private bool isShowingTransientEditor;
    private EditorHostState hostState = EditorHostState.Unloaded;
    private bool needsHostRestore;
    private bool isRestoringHost;
    private bool suppressBridgeCommands;
    private bool currentReadOnlyState;

    public event EventHandler<string>? ContentChanged;
    public event EventHandler<CreateExtractRequestedEventArgs>? CreateExtractRequested;
    public event EventHandler<SaveClozeRequestedEventArgs>? SaveClozeRequested;
    public event EventHandler<OpenImageOcclusionRequestedEventArgs>? OpenImageOcclusionRequested;
    public event EventHandler<MentionSearchRequestedEventArgs>? MentionSearchRequested;
    public event EventHandler<MentionPreviewRequestedEventArgs>? MentionPreviewRequested;
    public event EventHandler<OpenMentionRequestedEventArgs>? OpenMentionRequested;
    public event EventHandler<CreateLinkedNoteRequestedEventArgs>? CreateLinkedNoteRequested;
    public event Func<object?, AttachFileRequestedEventArgs, Task>? AttachFileRequested;
    public event Func<object?, ReadAttachmentRequestedEventArgs, Task>? ReadAttachmentRequested;
    public event EventHandler? RetryRequested;

    public bool IsShowingTransientEditor => isShowingTransientEditor;

    public EditorView(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        InitializeComponent();
        ConfigureBlockSuiteHost();
        BindBlockSuiteBridge();
        Loaded += EditorView_Loaded;
        Unloaded += EditorView_Unloaded;
        ShowNoNoteState();
    }

    public async Task WarmUpAsync()
    {
        await EnsureViewLoadedAsync();
        UpdateLayout();
        BlockSuiteHostView.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.Loaded);

        await WaitWithTimeoutAsync(
            blockSuiteBridge.WaitUntilReadyAsync(),
            HostStartupTimeout,
            "The BlockSuite host did not finish starting.");
        hostState = EditorHostState.Ready;
    }

    private async Task EnsureViewLoadedAsync()
    {
        if (IsLoaded)
        {
            return;
        }

        TaskCompletionSource loadedCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnLoaded(object? sender, RoutedEventArgs e)
        {
            loadedCompletion.TrySetResult();
        }

        Loaded += OnLoaded;

        try
        {
            if (IsLoaded)
            {
                return;
            }

            await WaitWithTimeoutAsync(
                loadedCompletion.Task,
                HostStartupTimeout,
                "The editor view did not finish loading.");
        }
        finally
        {
            Loaded -= OnLoaded;
        }
    }

    public async Task LoadNoteAsync(
        string title,
        string content,
        string? path = null,
        string? collectionRoot = null)
    {
        ClearTransientEditorState();
        int requestId = ++loadRequestId;
        ShowLoadingState("Loading note", "Opening the selected note...");
        UpdateCurrentNoteTitle(title);
        currentContent = string.Empty;
        currentSourcePath = null;
        currentCollectionRoot = null;

        await WarmUpAsync();

        if (requestId != loadRequestId)
        {
            ClearTransientEditorState();
            return;
        }

        currentSourcePath = path;
        currentCollectionRoot = collectionRoot;

        try
        {
            await WaitWithTimeoutAsync(
                blockSuiteBridge.LoadNoteAsync(path, title, content, collectionRoot),
                GetDocumentLoadTimeout(content),
                "The editor did not finish loading the note.");
        }
        catch
        {
            if (requestId == loadRequestId)
            {
                currentSourcePath = null;
                currentCollectionRoot = null;
            }

            throw;
        }

        if (requestId != loadRequestId)
        {
            return;
        }

        currentContent = content;
        needsHostRestore = false;
        SetBlockSuiteHostVisible(true);
        ShowReadyState();
        await FlushMentionTargetsAsync();
    }

    public async Task SetReadOnlyAsync(bool isReadOnly)
    {
        currentReadOnlyState = isReadOnly;
        if (string.IsNullOrWhiteSpace(currentSourcePath))
        {
            return;
        }

        await WaitWithTimeoutAsync(
            blockSuiteBridge.SetReadOnlyModeAsync(isReadOnly),
            EditorOperationTimeout,
            "The editor did not change modes.");
    }

    public void PostExtractResult(bool ok, string? createdPath = null, string? error = null)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.ShowExtractResultAsync(ok, createdPath, error),
            "show extract result");
    }

    public void PostMentionTargets(IEnumerable<KnowledgeMentionTargetReadModel> targets)
    {
        mentionTargets = targets.ToList();
        RunBlockSuiteCommand(FlushMentionTargetsAsync(), "flush mention targets");
    }

    public void PostMentionPreviewResult(
        string requestId,
        string relativePath,
        string mode,
        bool ok,
        string? documentContent = null,
        string? error = null)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.HandleMentionPreviewResultAsync(
                requestId,
                relativePath,
                mode,
                ok,
                documentContent,
                error),
            "post mention preview result");
    }

    public void PostDocumentLinks(KnowledgeDocumentLinksReadModel links)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.SetDocumentLinksAsync(links),
            "set document links");
    }

    public void PostMentionOpenResult(bool ok, string? error = null)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.HandleMentionOpenResultAsync(ok, error),
            "post mention open result");
    }

    public void PostSaveResult(bool ok, string? error = null)
    {
        RunBlockSuiteCommand(
            blockSuiteBridge.ShowSaveResultAsync(ok, error),
            "show save result");
    }

    public Task<SelectedImageBlockReadModel?> GetSelectedImageBlockAsync()
    {
        return blockSuiteBridge.GetSelectedImageBlockAsync();
    }

    public Task<SelectedCardContextBlocksReadModel?> GetSelectedCardContextBlocksAsync()
    {
        return blockSuiteBridge.GetSelectedCardContextBlocksAsync();
    }

    public Task CreateClozeFromSelectionAsync(
        string? cardContextId = null,
        string? slotId = null,
        int? displayClozeIndex = null)
    {
        return blockSuiteBridge.CreateClozeFromSelectionAsync(cardContextId, slotId, displayClozeIndex);
    }

    public async Task FocusBlockAsync(string blockId)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return;
        }

        if (!IsHostReady)
        {
            await WarmUpAsync();
        }

        await WaitWithTimeoutAsync(
            blockSuiteBridge.FocusBlockAsync(blockId),
            EditorOperationTimeout,
            "The editor did not navigate to the selected heading.");
    }

    public bool IsHostReady => hostState == EditorHostState.Ready && blockSuiteBridge.IsReady;

    public bool NeedsHostRestore => needsHostRestore;

    private async void EditorView_Loaded(object sender, RoutedEventArgs e)
    {
        if (isRestoringHost || !needsHostRestore)
        {
            hostState = blockSuiteBridge.IsReady ? EditorHostState.Ready : EditorHostState.Loading;
            return;
        }

        await RestoreHostAfterSuspensionAsync();
    }

    private void EditorView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded && hostState == EditorHostState.Unloaded)
        {
            return;
        }

        hostState = blockSuiteBridge.IsReady ? EditorHostState.Ready : EditorHostState.Unloaded;
    }

    private async Task RestoreHostAfterSuspensionAsync()
    {
        if (isRestoringHost)
        {
            return;
        }

        isRestoringHost = true;
        hostState = EditorHostState.Loading;
        ShowLoadingState("Restoring editor", "Reattaching the note editor...");

        try
        {
            await WarmUpAsync();

            if (!string.IsNullOrWhiteSpace(currentSourcePath))
            {
                await WaitWithTimeoutAsync(
                    blockSuiteBridge.LoadNoteAsync(currentSourcePath, currentTitle, currentContent, currentCollectionRoot),
                    GetDocumentLoadTimeout(currentContent),
                    "The editor did not restore the floating note.");
                await FlushMentionTargetsAsync();
                await blockSuiteBridge.SetDocumentLinksAsync(new KnowledgeDocumentLinksReadModel([], []));
                if (currentReadOnlyState)
                {
                    await blockSuiteBridge.SetReadOnlyModeAsync(true);
                }
            }

            suppressBridgeCommands = false;
            needsHostRestore = false;
            hostState = EditorHostState.Ready;
            ShowReadyState();
        }
        catch (Exception ex)
        {
            hostState = EditorHostState.Faulted;
            ShowFailedState(ex.Message, canRetry: true);
        }
        finally
        {
            isRestoringHost = false;
        }
    }
}
