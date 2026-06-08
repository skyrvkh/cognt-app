using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Abstractions;
using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Services;
using Microsoft.Extensions.Logging;

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

    private readonly BlockSuiteHostBridge blockSuiteBridge;
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<EditorView> logger;
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
    private EditorTheme? currentTheme;

    public event EventHandler<string>? ContentChanged;
    public event EventHandler? DocumentChanged;
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

    public EditorView(
        IServiceProvider serviceProvider,
        ILogger<EditorView> logger,
        ILogger<BlockSuiteHostBridge> bridgeLogger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        blockSuiteBridge = new BlockSuiteHostBridge(bridgeLogger);
        this.logger.LogInformation("EditorView constructed.");
        InitializeComponent();
        ConfigureBlockSuiteHost();
        BindBlockSuiteBridge();
        Loaded += EditorView_Loaded;
        Unloaded += EditorView_Unloaded;
        ShowNoNoteState();
    }

    public async Task WarmUpAsync()
    {
        logger.LogDebug(
            "EditorView warmup started. HostState={HostState} BridgeReady={BridgeReady}",
            hostState,
            blockSuiteBridge.IsReady);
        await EnsureViewLoadedAsync();
        UpdateLayout();
        BlockSuiteHostView.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.Loaded);

        await WaitWithTimeoutAsync(
            blockSuiteBridge.WaitUntilReadyAsync(),
            HostStartupTimeout,
            "The BlockSuite host did not finish starting.");
        hostState = EditorHostState.Ready;
        logger.LogInformation("EditorView warmup completed. HostState={HostState}", hostState);

        if (currentTheme is not null)
        {
            await WaitWithTimeoutAsync(
                blockSuiteBridge.ApplyThemeAsync(currentTheme),
                EditorOperationTimeout,
                "The editor did not apply the active theme.");
        }
    }

    private async Task EnsureViewLoadedAsync()
    {
        if (IsLoaded)
        {
            logger.LogDebug("EditorView already loaded.");
            return;
        }

        logger.LogDebug("Waiting for EditorView Loaded event.");
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
        logger.LogInformation(
            "EditorView LoadNote requested. Path={Path} Title={Title} ContentLength={ContentLength} CollectionRoot={CollectionRoot}",
            path,
            title,
            content.Length,
            collectionRoot);
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
            logger.LogWarning(
                "EditorView LoadNote superseded before bridge load. Path={Path} RequestId={RequestId} CurrentRequestId={CurrentRequestId}",
                path,
                requestId,
                loadRequestId);
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
        catch (Exception ex)
        {
            logger.LogError(ex, "EditorView LoadNote failed. Path={Path} RequestId={RequestId}", path, requestId);
            if (requestId == loadRequestId)
            {
                currentSourcePath = null;
                currentCollectionRoot = null;
            }

            throw;
        }

        if (requestId != loadRequestId)
        {
            logger.LogWarning(
                "EditorView LoadNote superseded after bridge load. Path={Path} RequestId={RequestId} CurrentRequestId={CurrentRequestId}",
                path,
                requestId,
                loadRequestId);
            return;
        }

        currentContent = content;
        needsHostRestore = false;
        SetBlockSuiteHostVisible(true);
        ShowReadyState();
        await FlushMentionTargetsAsync();
        logger.LogInformation("EditorView LoadNote completed. Path={Path} RequestId={RequestId}", path, requestId);
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

    public void ApplyTheme(EditorTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        currentTheme = theme;
        RunBlockSuiteCommand(
            blockSuiteBridge.ApplyThemeAsync(theme),
            "theme update");
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
        logger.LogDebug(
            "EditorView Loaded event. NeedsHostRestore={NeedsHostRestore} IsRestoringHost={IsRestoringHost} BridgeReady={BridgeReady}",
            needsHostRestore,
            isRestoringHost,
            blockSuiteBridge.IsReady);
        if (isRestoringHost || !needsHostRestore)
        {
            hostState = blockSuiteBridge.IsReady ? EditorHostState.Ready : EditorHostState.Loading;
            return;
        }

        await RestoreHostAfterSuspensionAsync();
    }

    private void EditorView_Unloaded(object sender, RoutedEventArgs e)
    {
        logger.LogDebug(
            "EditorView Unloaded event. HostState={HostState} BridgeReady={BridgeReady}",
            hostState,
            blockSuiteBridge.IsReady);
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
            logger.LogDebug("EditorView restore skipped because restore is already running.");
            return;
        }

        logger.LogInformation("EditorView restore started. Path={Path}", currentSourcePath);
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
            logger.LogError(ex, "EditorView restore failed. Path={Path}", currentSourcePath);
            hostState = EditorHostState.Faulted;
            ShowFailedState(ex.Message, canRetry: true);
        }
        finally
        {
            isRestoringHost = false;
            logger.LogDebug("EditorView restore finished. HostState={HostState}", hostState);
        }
    }
}
