using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using AvalonDock.Layout;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Wpf.EditorHost;
using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.Features.NoteToCard.ImageOcclusion;
using CogntApp.Shell.Dialogs;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Workspace;

internal sealed class NoteWorkspaceDocumentSession
{
    private const int LargeDocumentContentLength = 200_000;
    private static readonly TimeSpan EditorViewLoadTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DocumentLinksRefreshDelay = TimeSpan.FromMilliseconds(750);

    private readonly MainViewModel viewModel;
    private readonly IEditorViewFactory editorViewFactory;
    private readonly ContentControl dockedHost = new();
    private readonly IShellDialogService dialogs;
    private readonly IImageMaskEditorDialogService imageMaskEditorDialogs;
    private readonly Func<string, bool, Task> openNoteAsync;
    private readonly Action mentionTargetsChanged;
    private readonly Func<EditorTheme> getEditorTheme;
    private readonly ILogger<NoteWorkspaceDocumentSession> logger;

    private EditorView? editorView;
    private ContentControl? activeHost;
    private DetachedNoteWindow? detachedWindow;
    private string currentContent;
    private string lastPersistedContent;
    private NoteWorkspaceDocumentState state = NoteWorkspaceDocumentState.Docked;
    private bool isInitialized;
    private bool isMountingEditor;
    private bool isLoadingEditorNote;
    private bool isClosingDetachedWindowForRedock;
    private bool currentReadOnlyState;
    private bool hasPendingContentChanges;
    private int documentLinksRefreshVersion;
    private int autoSaveVersion;
    private CancellationTokenSource? scheduledDocumentLinksRefreshCancellation;
    private CancellationTokenSource? scheduledAutoSaveCancellation;

    internal NoteWorkspaceDocumentSession(
        MainViewModel viewModel,
        IEditorViewFactory editorViewFactory,
        IShellDialogService dialogs,
        IImageMaskEditorDialogService imageMaskEditorDialogs,
        Func<string, bool, Task> openNoteAsync,
        Action mentionTargetsChanged,
        Func<EditorTheme> getEditorTheme,
        ILogger<NoteWorkspaceDocumentSession> logger,
        PreparedNote note)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.editorViewFactory = editorViewFactory ?? throw new ArgumentNullException(nameof(editorViewFactory));
        this.dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        this.imageMaskEditorDialogs = imageMaskEditorDialogs ?? throw new ArgumentNullException(nameof(imageMaskEditorDialogs));
        this.openNoteAsync = openNoteAsync ?? throw new ArgumentNullException(nameof(openNoteAsync));
        this.mentionTargetsChanged = mentionTargetsChanged ?? throw new ArgumentNullException(nameof(mentionTargetsChanged));
        this.getEditorTheme = getEditorTheme ?? throw new ArgumentNullException(nameof(getEditorTheme));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(note);

        NotePath = note.NotePath;
        ItemId = note.ItemId;
        Title = note.Title;
        currentContent = note.Content;
        lastPersistedContent = note.Content;
        viewModel.AutoSaveSettingsChanged += ViewModel_AutoSaveSettingsChanged;

        LayoutDocument = new LayoutDocument
        {
            Title = note.Title,
            ContentId = note.NotePath,
            CanClose = true,
            CanFloat = false,
            Content = dockedHost,
        };
        LayoutDocument.Closed += LayoutDocument_Closed;
        this.logger.LogDebug(
            "Created note workspace session. NotePath={NotePath} Title={Title} ContentLength={ContentLength}",
            NotePath,
            Title,
            currentContent.Length);
    }

    internal LayoutDocument LayoutDocument { get; }

    internal event EventHandler? Closed;

    internal event EventHandler? Activated;

    internal string NotePath { get; }

    internal long? ItemId { get; }

    internal string Title { get; private set; }

    internal bool IsDocked => state == NoteWorkspaceDocumentState.Docked;

    internal bool IsDetached => state == NoteWorkspaceDocumentState.Detached;

    internal bool IsClosed => state == NoteWorkspaceDocumentState.Closed;

    internal bool HasMountedEditor => editorView is not null
        && activeHost is not null
        && ReferenceEquals(activeHost.Content, editorView);

    internal bool IsHostReady => editorView?.IsHostReady == true;

    internal string GetContent() => editorView?.GetContent() ?? currentContent;

    internal void ApplyTheme(EditorTheme theme)
    {
        editorView?.ApplyTheme(theme);
    }

    internal bool OwnsDockedContent(object? content)
    {
        return ReferenceEquals(LayoutDocument.Content, content)
            || ReferenceEquals(dockedHost, content);
    }

    internal async Task InitializeAsync()
    {
        logger.LogDebug("Initializing session. NotePath={NotePath}", NotePath);
        isInitialized = true;
        activeHost = dockedHost;
        await MountEditorIntoHostAsync(dockedHost);
        logger.LogInformation("Session initialized. NotePath={NotePath} HasMountedEditor={HasMountedEditor}", NotePath, HasMountedEditor);
    }

    internal async Task MountDockedAsync(LayoutDocumentPane documentPane)
    {
        logger.LogInformation("Mounting docked session. NotePath={NotePath}", NotePath);
        if (IsClosed)
        {
            logger.LogWarning("Skipped docked mount because session is closed. NotePath={NotePath}", NotePath);
            return;
        }

        currentContent = GetContent();
        UnmountEditor();
        CloseDetachedWindowForRedock();
        state = NoteWorkspaceDocumentState.Docked;

        if (!documentPane.Children.Contains(LayoutDocument))
        {
            documentPane.Children.Add(LayoutDocument);
        }

        LayoutDocument.IsSelected = true;
        LayoutDocument.IsActive = true;
        activeHost = dockedHost;
        await MountEditorIntoHostAsync(dockedHost);
    }

    internal async Task DetachToWindowAsync(
        Window owner,
        LayoutDocumentPane documentPane,
        Point screenPoint,
        Func<NoteWorkspaceDocumentSession, Task> redockAsync,
        Func<Rect, bool> shouldRedock)
    {
        logger.LogInformation("Detaching session to window. NotePath={NotePath}", NotePath);
        if (!IsDocked || IsClosed)
        {
            logger.LogWarning("Skipped detach. NotePath={NotePath} IsDocked={IsDocked} IsClosed={IsClosed}", NotePath, IsDocked, IsClosed);
            return;
        }

        currentContent = GetContent();
        UnmountEditor();
        state = NoteWorkspaceDocumentState.Detached;

        if (documentPane.Children.Contains(LayoutDocument))
        {
            documentPane.Children.Remove(LayoutDocument);
        }

        DetachedNoteWindow window = new(Title)
        {
            Owner = owner,
            Left = Math.Max(0, screenPoint.X - 140),
            Top = Math.Max(0, screenPoint.Y - 18),
        };
        window.HeaderDragCompleted += async (_, e) =>
        {
            if (shouldRedock(e.ScreenBounds) || shouldRedock(new Rect(e.DropScreenPoint, new Size(1, 1))))
            {
                await redockAsync(this);
            }
        };
        window.Activated += (_, _) => Activated?.Invoke(this, EventArgs.Empty);
        window.Closed += DetachedWindow_Closed;

        detachedWindow = window;
        activeHost = window.EditorHost;
        window.Show();
        window.Activate();
        Activated?.Invoke(this, EventArgs.Empty);
        await MountEditorIntoHostAsync(window.EditorHost);
    }

    internal async Task RedockAsync(LayoutDocumentPane documentPane)
    {
        logger.LogInformation("Redocking session. NotePath={NotePath}", NotePath);
        if (!IsDetached || IsClosed)
        {
            logger.LogWarning("Skipped redock. NotePath={NotePath} IsDetached={IsDetached} IsClosed={IsClosed}", NotePath, IsDetached, IsClosed);
            return;
        }

        currentContent = GetContent();
        UnmountEditor();
        CloseDetachedWindowForRedock();
        state = NoteWorkspaceDocumentState.Docked;

        if (!documentPane.Children.Contains(LayoutDocument))
        {
            documentPane.Children.Add(LayoutDocument);
        }

        LayoutDocument.IsSelected = true;
        LayoutDocument.IsActive = true;
        activeHost = dockedHost;
        await MountEditorIntoHostAsync(dockedHost);
        Activated?.Invoke(this, EventArgs.Empty);
    }

    internal async Task CloseAsync(bool savePendingChanges = true)
    {
        logger.LogInformation("Closing session. NotePath={NotePath}", NotePath);
        if (IsClosed)
        {
            logger.LogDebug("Close skipped because session already closed. NotePath={NotePath}", NotePath);
            return;
        }

        if (savePendingChanges && viewModel.CanSaveOnClose)
        {
            await SavePendingChangesAsync();
        }

        state = NoteWorkspaceDocumentState.Closed;
        currentContent = GetContent();
        CancelScheduledAutoSave();
        UnmountEditor();
        CloseDetachedWindowForRedock();
        dockedHost.Content = null;
        await Task.CompletedTask;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    internal Task SavePendingChangesAsync()
    {
        return SavePendingChangesAsync(postResult: false, reason: "close");
    }

    internal void SavePendingChanges()
    {
        CancelScheduledAutoSave();
        currentContent = GetContent();
        if (!HasPendingContentChanges())
        {
            return;
        }

        viewModel.SaveNoteIfChanged(NotePath, currentContent);
        MarkContentPersisted(currentContent);
    }

    internal bool Activate(bool selectInTree)
    {
        logger.LogDebug("Activating session. NotePath={NotePath} SelectInTree={SelectInTree}", NotePath, selectInTree);
        viewModel.GetCurrentEditorContent = GetContent;
        if (string.Equals(viewModel.CurrentNotePath, NotePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        viewModel.SetActiveWorkspaceNote(NotePath, Title, GetContent(), ItemId, lastPersistedContent);

        if (selectInTree)
        {
            // tree selection is managed by the window on activation
        }

        return true;
    }

    internal void Focus()
    {
        if (IsDetached)
        {
            detachedWindow?.Activate();
            return;
        }

        if (!LayoutDocument.IsSelected)
        {
            LayoutDocument.IsSelected = true;
        }

        if (!LayoutDocument.IsActive)
        {
            LayoutDocument.IsActive = true;
        }
    }

    internal void UpdateTitle(string title)
    {
        Title = string.IsNullOrWhiteSpace(title) ? Title : title.Trim();
        LayoutDocument.Title = Title;
        detachedWindow?.UpdateTitle(Title);
        editorView?.UpdateCurrentNoteTitle(Title);
    }

    internal async Task RefreshMentionTargetsAsync()
    {
        if (editorView is null || !IsHostReady)
        {
            return;
        }

        IReadOnlyList<KnowledgeMentionTargetReadModel> targets = await Task.Run(viewModel.ListMentionTargets);
        editorView.PostMentionTargets(targets);
    }

    internal void RefreshDocumentLinks()
    {
        CancelScheduledDocumentLinksRefresh();
        EditorView? activeEditor = editorView;
        if (activeEditor is null || !IsHostReady)
        {
            return;
        }

        string collectionPath;
        string relativePath;
        string content;

        try
        {
            Activate(selectInTree: false);
            collectionPath = viewModel.OpenCollectionPath;
            relativePath = viewModel.CurrentNoteRelativePath ?? string.Empty;
            content = GetContent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Document links refresh failed: {ex}");
            activeEditor.PostDocumentLinks(new KnowledgeDocumentLinksReadModel([], []));
            return;
        }

        int refreshVersion = ++documentLinksRefreshVersion;
        _ = Task.Run(() => viewModel.ReadDocumentLinks(collectionPath, relativePath, content))
            .ContinueWith(
                task => PostDocumentLinksResult(activeEditor, refreshVersion, task),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private void ScheduleDocumentLinksRefresh()
    {
        if (IsClosed)
        {
            return;
        }

        CancelScheduledDocumentLinksRefresh();
        CancellationTokenSource cancellation = new();
        CancellationToken cancellationToken = cancellation.Token;
        scheduledDocumentLinksRefreshCancellation = cancellation;

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(DocumentLinksRefreshDelay, cancellationToken);
                    RunScheduledDocumentLinksRefresh(cancellation, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
            },
            CancellationToken.None);
    }

    private void RunScheduledDocumentLinksRefresh(
        CancellationTokenSource cancellation,
        CancellationToken cancellationToken)
    {
        void RefreshIfCurrent()
        {
            if (cancellationToken.IsCancellationRequested
                || !ReferenceEquals(scheduledDocumentLinksRefreshCancellation, cancellation))
            {
                return;
            }

            scheduledDocumentLinksRefreshCancellation = null;
            cancellation.Dispose();
            RefreshDocumentLinks();
        }

        if (Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(RefreshIfCurrent);
            return;
        }

        RefreshIfCurrent();
    }

    private void CancelScheduledDocumentLinksRefresh()
    {
        if (scheduledDocumentLinksRefreshCancellation is null)
        {
            return;
        }

        scheduledDocumentLinksRefreshCancellation.Cancel();
        scheduledDocumentLinksRefreshCancellation.Dispose();
        scheduledDocumentLinksRefreshCancellation = null;
    }

    private void ScheduleAutoSave()
    {
        CancelScheduledAutoSave();
        if (IsClosed || !viewModel.CanRunAutoSave || !HasPendingContentChanges())
        {
            return;
        }

        CancellationTokenSource cancellation = new();
        CancellationToken cancellationToken = cancellation.Token;
        int saveVersion = ++autoSaveVersion;
        scheduledAutoSaveCancellation = cancellation;

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(viewModel.AutoSaveInterval, cancellationToken);
                    await RunScheduledAutoSaveAsync(cancellation, saveVersion, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
            },
            CancellationToken.None);
    }

    private Task RunScheduledAutoSaveAsync(
        CancellationTokenSource cancellation,
        int saveVersion,
        CancellationToken cancellationToken)
    {
        return InvokeOnUiThreadAsync(async () =>
        {
            if (cancellationToken.IsCancellationRequested
                || !ReferenceEquals(scheduledAutoSaveCancellation, cancellation)
                || saveVersion != autoSaveVersion)
            {
                return;
            }

            scheduledAutoSaveCancellation = null;
            cancellation.Dispose();

            try
            {
                await SavePendingChangesAsync(postResult: true, reason: "interval auto-save");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Interval auto-save failed. NotePath={NotePath}", NotePath);
                editorView?.PostSaveResult(ok: false, error: ex.Message);
            }
        });
    }

    private void CancelScheduledAutoSave()
    {
        autoSaveVersion++;
        if (scheduledAutoSaveCancellation is null)
        {
            return;
        }

        scheduledAutoSaveCancellation.Cancel();
        scheduledAutoSaveCancellation.Dispose();
        scheduledAutoSaveCancellation = null;
    }

    private async Task SavePendingChangesAsync(bool postResult, string reason)
    {
        CancelScheduledAutoSave();
        currentContent = await GetLatestContentAsync();
        if (!HasPendingContentChanges())
        {
            return;
        }

        string contentToSave = currentContent;
        Stopwatch? saveTiming = contentToSave.Length >= LargeDocumentContentLength
            ? Stopwatch.StartNew()
            : null;

        try
        {
            await viewModel.SaveNoteIfChangedAsync(NotePath, contentToSave);
            if (saveTiming is not null)
            {
                saveTiming.Stop();
                logger.LogDebug(
                    "Large note save completed. NotePath={NotePath} Reason={Reason} ContentLength={ContentLength} ElapsedMs={ElapsedMs}",
                    NotePath,
                    reason,
                    contentToSave.Length,
                    saveTiming.ElapsedMilliseconds);
            }

            MarkContentPersisted(contentToSave);
            if (postResult)
            {
                editorView?.PostSaveResult(ok: true);
            }
        }
        catch
        {
            if (postResult)
            {
                editorView?.PostSaveResult(ok: false, error: "The note could not be saved.");
            }

            throw;
        }
    }

    private Task<string> GetLatestContentAsync()
    {
        return editorView?.GetContentAsync() ?? Task.FromResult(currentContent);
    }

    private bool HasPendingContentChanges()
    {
        return hasPendingContentChanges
            || !string.Equals(currentContent, lastPersistedContent, StringComparison.Ordinal);
    }

    private void MarkContentPersisted(string content)
    {
        lastPersistedContent = content;
        hasPendingContentChanges = !string.Equals(currentContent, lastPersistedContent, StringComparison.Ordinal);
        if (hasPendingContentChanges && viewModel.CanRunAutoSave)
        {
            ScheduleAutoSave();
        }
    }

    private void ViewModel_AutoSaveSettingsChanged(object? sender, EventArgs e)
    {
        if (!viewModel.CanRunAutoSave)
        {
            CancelScheduledAutoSave();
            return;
        }

        if (HasPendingContentChanges())
        {
            ScheduleAutoSave();
        }
    }

    private static Task InvokeOnUiThreadAsync(Func<Task> action)
    {
        if (Application.Current?.Dispatcher is not { } dispatcher
            || dispatcher.CheckAccess())
        {
            return action();
        }

        return dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    private void PostDocumentLinksResult(
        EditorView targetEditor,
        int refreshVersion,
        Task<KnowledgeDocumentLinksReadModel> task)
    {
        void PostIfCurrent()
        {
            if (IsClosed
                || refreshVersion != documentLinksRefreshVersion
                || !ReferenceEquals(editorView, targetEditor))
            {
                return;
            }

            if (task.IsCompletedSuccessfully)
            {
                targetEditor.PostDocumentLinks(task.Result);
                return;
            }

            if (task.Exception is not null)
            {
                System.Diagnostics.Debug.WriteLine($"Document links refresh failed: {task.Exception}");
            }

            targetEditor.PostDocumentLinks(new KnowledgeDocumentLinksReadModel([], []));
        }

        if (Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(PostIfCurrent);
            return;
        }

        PostIfCurrent();
    }

    internal async Task CreateClozeFromSelectionAsync()
    {
        EditorView? activeEditor = editorView;
        if (activeEditor is null)
        {
            return;
        }

        Activate(selectInTree: false);
        await activeEditor.CreateClozeFromSelectionAsync();
    }

    internal async Task CreateCardContextFromSelectionAsync()
    {
        EditorView? activeEditor = editorView;
        if (activeEditor is null)
        {
            return;
        }

        Activate(selectInTree: false);
        SelectedCardContextBlocksReadModel? selection = await activeEditor.GetSelectedCardContextBlocksAsync();
        if (selection is null || selection.BlockIds.Count == 0)
        {
            dialogs.ShowWarning(
                "Card Context",
                "Select one or more note blocks before creating a card context.");
            return;
        }

        string content = GetContent();
        viewModel.CreateCardContextFromSelection(selection.BlockIds, selection.Title, content);
        MarkContentPersisted(content);
        activeEditor.PostSaveResult(ok: true);
    }

    internal async Task AddSelectedTextToSelectedCardSlotAsync()
    {
        EditorView? activeEditor = editorView;
        if (activeEditor is null)
        {
            return;
        }

        Activate(selectInTree: false);
        CardCandidateReadModel? candidate = viewModel.SelectedCardCandidate;
        if (candidate is null)
        {
            dialogs.ShowWarning("Note Cards", "Select a card candidate first.");
            return;
        }

        await activeEditor.CreateClozeFromSelectionAsync(
            candidate.ContextId,
            candidate.SlotId,
            candidate.DisplayClozeIndex);
    }

    internal async Task FocusBlockAsync(string blockId)
    {
        EditorView? activeEditor = editorView;
        if (activeEditor is null || string.IsNullOrWhiteSpace(blockId))
        {
            return;
        }

        Focus();
        await activeEditor.FocusBlockAsync(blockId);
    }

    internal async Task OpenImageOcclusionForSelectedImageAsync()
    {
        EditorView? activeEditor = editorView;
        if (activeEditor is null)
        {
            return;
        }

        Activate(selectInTree: false);
        SelectedImageBlockReadModel? selectedImage = await activeEditor.GetSelectedImageBlockAsync();
        if (selectedImage is null
            || string.IsNullOrWhiteSpace(selectedImage.ImageBlockId)
            || string.IsNullOrWhiteSpace(selectedImage.ImageRelPath))
        {
            dialogs.ShowWarning(
                "Image Occlusion",
                "Select an image block in the note before opening the image occlusion editor.");
            return;
        }

        await OpenImageOcclusionForImageAsync(
            selectedImage.ImageBlockId,
            selectedImage.ImageRelPath,
            GetContent());
    }

    internal Task SetReadOnlyAsync(bool isReadOnly)
    {
        currentReadOnlyState = isReadOnly;
        return editorView?.SetReadOnlyAsync(isReadOnly) ?? Task.CompletedTask;
    }

    internal async Task RefreshHostStateAsync()
    {
        if (!IsHostReady)
        {
            return;
        }

        await RefreshMentionTargetsAsync();
        RefreshDocumentLinks();
    }

    internal void Dispose()
    {
        logger.LogInformation("Disposing session. NotePath={NotePath} State={State} HasMountedEditor={HasMountedEditor}", NotePath, state, HasMountedEditor);
        LayoutDocument.Closed -= LayoutDocument_Closed;
        viewModel.AutoSaveSettingsChanged -= ViewModel_AutoSaveSettingsChanged;
        CancelScheduledDocumentLinksRefresh();
        CancelScheduledAutoSave();
        UnmountEditor();
        CloseDetachedWindowForRedock();
        dockedHost.Content = null;
    }

    private async void LayoutDocument_Closed(object? sender, EventArgs e)
    {
        if (!IsDocked)
        {
            return;
        }

        try
        {
            await CloseAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Close failed while saving note. NotePath={NotePath}", NotePath);
            dialogs.ShowWarning("Close Note Failed", $"The note could not be saved before closing: {ex.Message}");
        }
    }

    private async void DetachedWindow_Closed(object? sender, EventArgs e)
    {
        if (isClosingDetachedWindowForRedock || IsClosed)
        {
            return;
        }

        detachedWindow = null;
        try
        {
            await CloseAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Detached close failed while saving note. NotePath={NotePath}", NotePath);
            dialogs.ShowWarning("Close Note Failed", $"The note could not be saved before closing: {ex.Message}");
        }
    }

    private async Task MountEditorIntoHostAsync(ContentControl host)
    {
        logger.LogDebug(
            "Mount editor requested. NotePath={NotePath} IsInitialized={IsInitialized} IsClosed={IsClosed} IsMountingEditor={IsMountingEditor}",
            NotePath,
            isInitialized,
            IsClosed,
            isMountingEditor);
        if (!isInitialized || IsClosed || isMountingEditor)
        {
            logger.LogWarning(
                "Mount editor skipped. NotePath={NotePath} IsInitialized={IsInitialized} IsClosed={IsClosed} IsMountingEditor={IsMountingEditor}",
                NotePath,
                isInitialized,
                IsClosed,
                isMountingEditor);
            return;
        }

        isMountingEditor = true;

        try
        {
            logger.LogInformation("Creating EditorView for session. NotePath={NotePath}", NotePath);
            EditorView nextEditor = editorViewFactory.Create();
            nextEditor.ApplyTheme(getEditorTheme());
            WireEditorView(nextEditor);
            editorView = nextEditor;
            activeHost = host;
            host.Content = nextEditor;

            logger.LogDebug("Waiting for EditorView Loaded. NotePath={NotePath}", NotePath);
            await WaitForEditorLoadedAsync(nextEditor);
            logger.LogDebug("EditorView Loaded. NotePath={NotePath}", NotePath);
            isLoadingEditorNote = true;
            bool hadPendingChangesBeforeLoad = HasPendingContentChanges();
            logger.LogInformation(
                "Loading note into EditorView. NotePath={NotePath} Title={Title} ContentLength={ContentLength}",
                NotePath,
                Title,
                currentContent.Length);
            await nextEditor.LoadNoteAsync(
                Title,
                currentContent,
                NotePath,
                viewModel.OpenCollectionPath);
            currentContent = nextEditor.GetContent();
            if (!hadPendingChangesBeforeLoad)
            {
                lastPersistedContent = currentContent;
            }

            hasPendingContentChanges = hadPendingChangesBeforeLoad
                && !string.Equals(currentContent, lastPersistedContent, StringComparison.Ordinal);
            isLoadingEditorNote = false;
            logger.LogInformation("Note loaded into EditorView. NotePath={NotePath} CurrentContentLength={ContentLength}", NotePath, currentContent.Length);

            if (currentReadOnlyState)
            {
                await nextEditor.SetReadOnlyAsync(true);
            }

            await RefreshMentionTargetsAsync();
            RefreshDocumentLinks();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Mount editor failed. NotePath={NotePath}", NotePath);
            isLoadingEditorNote = false;
            if (ReferenceEquals(editorView, host.Content))
            {
                host.Content = null;
            }

            EditorView? failedEditor = editorView;
            UnwireEditorView(failedEditor);
            failedEditor?.DisposeEditorHost();
            editorView = null;
            if (ReferenceEquals(activeHost, host))
            {
                activeHost = null;
            }

            throw;
        }
        finally
        {
            isMountingEditor = false;
        }
    }

    private void UnmountEditor()
    {
        logger.LogDebug("Unmounting editor. NotePath={NotePath} HasEditor={HasEditor}", NotePath, editorView is not null);
        EditorView? existingEditor = editorView;
        isLoadingEditorNote = false;
        CancelScheduledDocumentLinksRefresh();
        currentContent = GetContent();
        UnwireEditorView(existingEditor);
        if (activeHost is not null)
        {
            activeHost.Content = null;
        }

        existingEditor?.DisposeEditorHost();
        editorView = null;
        activeHost = null;
    }

    private void CloseDetachedWindowForRedock()
    {
        if (detachedWindow is null)
        {
            return;
        }

        isClosingDetachedWindowForRedock = true;
        try
        {
            detachedWindow.Closed -= DetachedWindow_Closed;
            detachedWindow.Close();
        }
        finally
        {
            detachedWindow = null;
            isClosingDetachedWindowForRedock = false;
        }
    }

    private async Task WaitForEditorLoadedAsync(EditorView nextEditor)
    {
        if (nextEditor.IsLoaded)
        {
            logger.LogDebug("EditorView was already WPF-loaded. NotePath={NotePath}", NotePath);
            return;
        }

        logger.LogDebug("Waiting for EditorView WPF Loaded event. NotePath={NotePath}", NotePath);
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnLoaded(object? sender, RoutedEventArgs e)
        {
            nextEditor.Loaded -= OnLoaded;
            completion.TrySetResult();
        }

        nextEditor.Loaded += OnLoaded;
        try
        {
            Task completedTask = await Task.WhenAny(completion.Task, Task.Delay(EditorViewLoadTimeout));
            if (completedTask != completion.Task)
            {
                logger.LogError(
                    "EditorView WPF Loaded event timed out. NotePath={NotePath} Timeout={Timeout}",
                    NotePath,
                    EditorViewLoadTimeout);
                throw new TimeoutException("The editor view did not finish loading.");
            }

            await completion.Task;
            logger.LogDebug("EditorView WPF Loaded event observed. NotePath={NotePath}", NotePath);
        }
        finally
        {
            nextEditor.Loaded -= OnLoaded;
        }
    }

    private void WireEditorView(EditorView nextEditor)
    {
        nextEditor.DocumentChanged += EditorView_DocumentChanged;
        nextEditor.ContentChanged += EditorView_ContentChanged;
        nextEditor.CreateExtractRequested += EditorView_CreateExtractRequested;
        nextEditor.SaveClozeRequested += EditorView_SaveClozeRequested;
        nextEditor.OpenImageOcclusionRequested += EditorView_OpenImageOcclusionRequested;
        nextEditor.MentionSearchRequested += EditorView_MentionSearchRequested;
        nextEditor.MentionPreviewRequested += EditorView_MentionPreviewRequested;
        nextEditor.OpenMentionRequested += EditorView_OpenMentionRequested;
        nextEditor.CreateLinkedNoteRequested += EditorView_CreateLinkedNoteRequested;
        nextEditor.AttachFileRequested += EditorView_AttachFileRequested;
        nextEditor.ReadAttachmentRequested += EditorView_ReadAttachmentRequested;
    }

    private void UnwireEditorView(EditorView? existingEditor)
    {
        if (existingEditor is null)
        {
            return;
        }

        existingEditor.DocumentChanged -= EditorView_DocumentChanged;
        existingEditor.ContentChanged -= EditorView_ContentChanged;
        existingEditor.CreateExtractRequested -= EditorView_CreateExtractRequested;
        existingEditor.SaveClozeRequested -= EditorView_SaveClozeRequested;
        existingEditor.OpenImageOcclusionRequested -= EditorView_OpenImageOcclusionRequested;
        existingEditor.MentionSearchRequested -= EditorView_MentionSearchRequested;
        existingEditor.MentionPreviewRequested -= EditorView_MentionPreviewRequested;
        existingEditor.OpenMentionRequested -= EditorView_OpenMentionRequested;
        existingEditor.CreateLinkedNoteRequested -= EditorView_CreateLinkedNoteRequested;
        existingEditor.AttachFileRequested -= EditorView_AttachFileRequested;
        existingEditor.ReadAttachmentRequested -= EditorView_ReadAttachmentRequested;
    }

    private async void EditorView_ContentChanged(object? sender, string content)
    {
        if (isLoadingEditorNote)
        {
            currentContent = content;
            return;
        }

        currentContent = content;
        hasPendingContentChanges = !string.Equals(currentContent, lastPersistedContent, StringComparison.Ordinal);

        try
        {
            Activate(selectInTree: false);
            ScheduleDocumentLinksRefresh();
            ScheduleAutoSave();
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            editorView?.PostSaveResult(ok: false, error: ex.Message);
        }
    }

    private void EditorView_DocumentChanged(object? sender, EventArgs e)
    {
        if (isLoadingEditorNote)
        {
            return;
        }

        hasPendingContentChanges = true;

        try
        {
            Activate(selectInTree: false);
            ScheduleAutoSave();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Document dirty notification failed. NotePath={NotePath}", NotePath);
        }
    }

    private async void EditorView_CreateExtractRequested(object? sender, CreateExtractRequestedEventArgs e)
    {
        if (isLoadingEditorNote)
        {
            return;
        }

        try
        {
            Activate(selectInTree: false);
            string createdPath = await viewModel.CreateExtractNoteAsync(
                e.SourcePath,
                e.SelectedContent,
                e.SelectedPlainText);
            editorView?.PostExtractResult(ok: true, createdPath);
        }
        catch (Exception ex)
        {
            editorView?.PostExtractResult(ok: false, error: ex.Message);
        }
    }

    private void EditorView_SaveClozeRequested(object? sender, SaveClozeRequestedEventArgs e)
    {
        if (isLoadingEditorNote)
        {
            return;
        }

        currentContent = e.Content;

        try
        {
            Activate(selectInTree: false);
            viewModel.SaveClozeDraft(
                e.SourcePath,
                e.BlockId,
                e.SelectedPlainText,
                e.ClozeText,
                e.Content,
                e.CardContextId,
                e.SlotId,
                e.DisplayClozeIndex);
            MarkContentPersisted(currentContent);
            RefreshDocumentLinks();
            editorView?.PostSaveResult(ok: true);
        }
        catch (Exception ex)
        {
            editorView?.PostSaveResult(ok: false, error: ex.Message);
        }
    }

    private async void EditorView_OpenImageOcclusionRequested(object? sender, OpenImageOcclusionRequestedEventArgs e)
    {
        if (isLoadingEditorNote)
        {
            return;
        }

        currentContent = e.Content;
        await OpenImageOcclusionForImageAsync(e.ImageBlockId, e.ImageRelPath, e.Content);
    }

    private void EditorView_MentionSearchRequested(object? sender, MentionSearchRequestedEventArgs e)
    {
        if (isLoadingEditorNote)
        {
            e.Complete([]);
            return;
        }

        e.Complete(viewModel.SearchMentionTargets(e.Query, e.Limit));
    }

    private void EditorView_MentionPreviewRequested(object? sender, MentionPreviewRequestedEventArgs e)
    {
        if (isLoadingEditorNote)
        {
            editorView?.PostMentionPreviewResult(
                e.RequestId,
                e.RelativePath,
                e.Mode,
                ok: false,
                error: "The note is still loading.");
            return;
        }

        try
        {
            string preview = viewModel.ReadMentionPreview(e.RelativePath, e.MaxCharacters);
            editorView?.PostMentionPreviewResult(
                e.RequestId,
                e.RelativePath,
                e.Mode,
                ok: true,
                documentContent: preview);
        }
        catch (Exception ex)
        {
            editorView?.PostMentionPreviewResult(
                e.RequestId,
                e.RelativePath,
                e.Mode,
                ok: false,
                error: ex.Message);
        }
    }

    private async void EditorView_OpenMentionRequested(object? sender, OpenMentionRequestedEventArgs e)
    {
        if (isLoadingEditorNote)
        {
            editorView?.PostMentionOpenResult(ok: false, error: "The note is still loading.");
            return;
        }

        currentContent = e.Content;

        try
        {
            Activate(selectInTree: false);
            viewModel.SaveMentionSource(e.SourcePath, e.Content);
            MarkContentPersisted(currentContent);
            if (viewModel.TryOpenPdfDocumentByRelativePath(e.RelativePath))
            {
                editorView?.PostMentionOpenResult(ok: true);
                return;
            }

            string targetPath = viewModel.ResolveMentionPath(e.RelativePath);
            await openNoteAsync(targetPath, true);
            editorView?.PostMentionOpenResult(ok: true);
        }
        catch (Exception ex)
        {
            editorView?.PostMentionOpenResult(ok: false, error: ex.Message);
        }
    }

    private void EditorView_CreateLinkedNoteRequested(object? sender, CreateLinkedNoteRequestedEventArgs e)
    {
        if (isLoadingEditorNote)
        {
            e.Complete(CreateLinkedNoteResult.Fail("The note is still loading."));
            return;
        }

        try
        {
            Activate(selectInTree: false);
            CreateLinkedNoteResult result = viewModel.CreateLinkedNote(e.SourcePath);
            e.Complete(result);
            mentionTargetsChanged();
        }
        catch (Exception ex)
        {
            e.Complete(CreateLinkedNoteResult.Fail(ex.Message));
        }
    }

    private async Task EditorView_AttachFileRequested(object? sender, AttachFileRequestedEventArgs e)
    {
        if (isLoadingEditorNote)
        {
            e.Complete(AttachFileResult.Fail("The note is still loading."));
            return;
        }

        try
        {
            Activate(selectInTree: false);
            byte[] content = Convert.FromBase64String(e.ContentBase64);
            using MemoryStream stream = new(content, writable: false);
            KnowledgeAttachmentReadModel attachment = await viewModel.AttachAttachmentStreamToCurrentNoteAsync(
                e.FileName,
                stream);

            e.Complete(AttachFileResult.Success(
                attachment.OriginalFileName,
                attachment.RelativePath,
                attachment.Kind.ToString()));
        }
        catch (Exception ex)
        {
            e.Complete(AttachFileResult.Fail(ex.Message));
        }
    }

    private async Task EditorView_ReadAttachmentRequested(object? sender, ReadAttachmentRequestedEventArgs e)
    {
        if (isLoadingEditorNote)
        {
            e.Complete(ReadAttachmentResult.Fail("The note is still loading."));
            return;
        }

        try
        {
            Activate(selectInTree: false);
            ReadAttachmentResult result = await viewModel.ReadImageAttachmentForCurrentNoteAsync(
                e.SourcePath,
                e.ImageSource);
            e.Complete(result);
        }
        catch (Exception ex)
        {
            e.Complete(ReadAttachmentResult.Fail(ex.Message));
        }
    }

    private async Task OpenImageOcclusionForImageAsync(
        string imageBlockId,
        string imageRelPath,
        string content)
    {
        try
        {
            Activate(selectInTree: false);
            OpenImageMaskEditorRequest request = viewModel.CreateImageMaskEditorRequest(
                imageBlockId,
                imageRelPath,
                content);
            MarkContentPersisted(content);
            ImageMaskEditorResult result = await imageMaskEditorDialogs.ShowAsync(
                request,
                async applied =>
                {
                    viewModel.SaveImageMaskEditorResult(applied, content);
                    MarkContentPersisted(content);
                    editorView?.PostSaveResult(ok: true);
                    await Task.CompletedTask;
                });
            if (!result.Saved)
            {
                return;
            }

            viewModel.SaveImageMaskEditorResult(result, content);
            MarkContentPersisted(content);
            editorView?.PostSaveResult(ok: true);
        }
        catch (Exception ex)
        {
            dialogs.ShowWarning("Image Occlusion", ex.Message);
        }
    }
}
