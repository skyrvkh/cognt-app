using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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
    private static readonly TimeSpan EditorViewLoadTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DocumentLinksRefreshDelay = TimeSpan.FromMilliseconds(750);
    private static readonly HttpClient PastedImageHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

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
    private EditorCaretState? lastKnownCaretState;
    private EditorCaretState? pendingRestoreCaretState;

    internal NoteWorkspaceDocumentSession(
        MainViewModel viewModel,
        IEditorViewFactory editorViewFactory,
        IShellDialogService dialogs,
        IImageMaskEditorDialogService imageMaskEditorDialogs,
        Func<string, bool, Task> openNoteAsync,
        Action mentionTargetsChanged,
        Func<EditorTheme> getEditorTheme,
        ILogger<NoteWorkspaceDocumentSession> logger,
        PreparedNote note,
        EditorCaretState? restoreCaretState = null)
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
        lastKnownCaretState = NormalizeCaretState(restoreCaretState);
        pendingRestoreCaretState = lastKnownCaretState;
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

    internal bool HasAttachedEditor => editorView is not null
        && activeHost is not null
        && ReferenceEquals(activeHost.Content, editorView);

    internal bool IsHostReady => editorView?.IsHostReady == true;

    internal bool HasMountedEditor => HasAttachedEditor && IsHostReady;

    internal string GetContent()
    {
        if (editorView is null || isLoadingEditorNote || !IsHostReady)
        {
            return currentContent;
        }

        return editorView.GetContent();
    }

    internal async Task<EditorCaretState?> GetCaretStateAsync()
    {
        if (editorView?.IsHostReady != true)
        {
            return lastKnownCaretState;
        }

        try
        {
            EditorCaretState? caret = NormalizeCaretState(await editorView.GetCaretStateAsync());
            if (caret is not null)
            {
                lastKnownCaretState = caret;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Editor caret state read failed. NotePath={NotePath}", NotePath);
        }

        return lastKnownCaretState;
    }

    internal async Task RestoreCaretStateAsync(EditorCaretState? caret)
    {
        pendingRestoreCaretState = NormalizeCaretState(caret);
        if (pendingRestoreCaretState is null || editorView?.IsHostReady != true)
        {
            return;
        }

        await RestorePendingCaretStateAsync(editorView);
    }

    internal async Task ReplaceContentAsync(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        currentContent = content;
        lastPersistedContent = content;
        hasPendingContentChanges = false;

        if (editorView is null)
        {
            return;
        }

        isLoadingEditorNote = true;
        try
        {
            await editorView.LoadNoteAsync(
                Title,
                content,
                NotePath,
                viewModel.OpenCollectionPath);
            currentContent = editorView.GetContent();
            MarkContentPersisted(currentContent);

            if (currentReadOnlyState)
            {
                await editorView.SetReadOnlyAsync(true);
            }

            await RefreshMentionTargetsAsync();
            RefreshDocumentLinks();
        }
        finally
        {
            isLoadingEditorNote = false;
        }
    }

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
        isInitialized = true;
        activeHost = dockedHost;
        await MountEditorIntoHostAsync(dockedHost);
    }

    internal async Task MountDockedAsync(LayoutDocumentPane documentPane)
    {
        if (IsClosed)
        {
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
        if (!IsDocked || IsClosed)
        {
            return;
        }

        currentContent = GetContent();
        UnmountEditor();
        state = NoteWorkspaceDocumentState.Detached;

        if (documentPane.Children.Contains(LayoutDocument))
        {
            documentPane.Children.Remove(LayoutDocument);
        }

        DetachedNoteWindow window = new(Title, logger)
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
        if (!IsDetached || IsClosed)
        {
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
        if (IsClosed)
        {
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
        return SavePendingChangesAsync(postResult: false);
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

    internal void Focus(bool focusEditor = true)
    {
        if (IsDetached)
        {
            detachedWindow?.Activate();
            if (focusEditor)
            {
                _ = FocusEditorHostAsync();
            }

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

        if (focusEditor)
        {
            _ = FocusEditorHostAsync();
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
            logger.LogWarning(ex, "Document links refresh failed before reading editor content. NotePath={NotePath}", NotePath);
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
        return WorkspaceDispatcher.InvokeAsync(async () =>
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
                await SavePendingChangesAsync(postResult: true);
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

    private async Task SavePendingChangesAsync(bool postResult)
    {
        CancelScheduledAutoSave();
        currentContent = await GetLatestContentAsync();
        if (!HasPendingContentChanges())
        {
            return;
        }

        string contentToSave = currentContent;

        try
        {
            await viewModel.SaveNoteIfChangedAsync(NotePath, contentToSave);
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
        if (editorView is null || isLoadingEditorNote || !IsHostReady)
        {
            return Task.FromResult(currentContent);
        }

        return editorView.GetContentAsync();
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
                logger.LogWarning(task.Exception, "Document links refresh failed while reading links. NotePath={NotePath}", NotePath);
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
        await Task.CompletedTask;
        dialogs.ShowWarning("Card Context", "Card context creation is handled from the note card queue.");
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
        if (!isInitialized || IsClosed || isMountingEditor)
        {
            return;
        }

        isMountingEditor = true;
        EditorView? nextEditor = null;

        try
        {
            nextEditor = editorViewFactory.Create();
            nextEditor.ApplyTheme(getEditorTheme());
            WireEditorView(nextEditor);
            editorView = nextEditor;
            activeHost = host;
            host.Content = nextEditor;

            await WorkspaceEditorViewLoadAwaiter.WaitAsync(nextEditor, EditorViewLoadTimeout, logger);
            if (!IsCurrentMount(nextEditor, host))
            {
                return;
            }

            isLoadingEditorNote = true;
            try
            {
                bool hadPendingChangesBeforeLoad = HasPendingContentChanges();
                await nextEditor.LoadNoteAsync(
                    Title,
                    currentContent,
                    NotePath,
                    viewModel.OpenCollectionPath);
                if (!IsCurrentMount(nextEditor, host))
                {
                    return;
                }

                await RestorePendingCaretStateAsync(nextEditor);
                if (!IsCurrentMount(nextEditor, host))
                {
                    return;
                }

                currentContent = nextEditor.GetContent();
                if (!hadPendingChangesBeforeLoad)
                {
                    lastPersistedContent = currentContent;
                }

                hasPendingContentChanges = hadPendingChangesBeforeLoad
                    && !string.Equals(currentContent, lastPersistedContent, StringComparison.Ordinal);
            }
            finally
            {
                isLoadingEditorNote = false;
            }

            if (currentReadOnlyState)
            {
                await nextEditor.SetReadOnlyAsync(true);
            }

            await RefreshMentionTargetsAsync();
            RefreshDocumentLinks();
        }
        catch (Exception ex) when (nextEditor is not null && !IsCurrentMount(nextEditor, host))
        {
            logger.LogDebug(
                ex,
                "Editor mount stopped because the note session was closed or superseded. NotePath={NotePath}",
                NotePath);
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

    private bool IsCurrentMount(EditorView editor, ContentControl host)
    {
        return !IsClosed
            && ReferenceEquals(editorView, editor)
            && ReferenceEquals(activeHost, host)
            && ReferenceEquals(host.Content, editor);
    }

    private void UnmountEditor()
    {
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
        nextEditor.AttachImageSourceRequested += EditorView_AttachImageSourceRequested;
        nextEditor.ReadAttachmentRequested += EditorView_ReadAttachmentRequested;
        nextEditor.StoreEditorBlobRequested += EditorView_StoreEditorBlobRequested;
        nextEditor.ReadEditorBlobRequested += EditorView_ReadEditorBlobRequested;
        nextEditor.ListEditorBlobsRequested += EditorView_ListEditorBlobsRequested;
    }

    private async Task RestorePendingCaretStateAsync(EditorView targetEditor)
    {
        EditorCaretState? caret = pendingRestoreCaretState;
        if (caret?.IsValid != true)
        {
            return;
        }

        try
        {
            await targetEditor.RestoreCaretStateAsync(caret);
            lastKnownCaretState = caret;
            pendingRestoreCaretState = null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Editor caret state restore failed. NotePath={NotePath}", NotePath);
        }
    }

    private async Task FocusEditorHostAsync()
    {
        try
        {
            if (editorView is null || !IsHostReady)
            {
                return;
            }

            await editorView.FocusEditorAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Editor focus failed. NotePath={NotePath}", NotePath);
        }
    }

    private static EditorCaretState? NormalizeCaretState(EditorCaretState? caret)
    {
        if (caret?.IsValid != true)
        {
            return null;
        }

        return new EditorCaretState
        {
            BlockId = caret.BlockId.Trim(),
            Index = Math.Max(0, caret.Index),
            Length = Math.Max(0, caret.Length),
        };
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
        existingEditor.AttachImageSourceRequested -= EditorView_AttachImageSourceRequested;
        existingEditor.ReadAttachmentRequested -= EditorView_ReadAttachmentRequested;
        existingEditor.StoreEditorBlobRequested -= EditorView_StoreEditorBlobRequested;
        existingEditor.ReadEditorBlobRequested -= EditorView_ReadEditorBlobRequested;
        existingEditor.ListEditorBlobsRequested -= EditorView_ListEditorBlobsRequested;
    }

    private async void EditorView_ContentChanged(object? sender, string content)
    {
        if (IsClosed || !ReferenceEquals(sender, editorView))
        {
            return;
        }

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
            logger.LogWarning(ex, "Editor content-change handling failed. NotePath={NotePath}", NotePath);
            editorView?.PostSaveResult(ok: false, error: ex.Message);
        }
    }

    private void EditorView_DocumentChanged(object? sender, EventArgs e)
    {
        if (IsClosed || !ReferenceEquals(sender, editorView))
        {
            return;
        }

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
            logger.LogError(ex, "Create extract note request failed. NotePath={NotePath} SourcePath={SourcePath}", NotePath, e.SourcePath);
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
            logger.LogError(ex, "Editor cloze save failed. NotePath={NotePath} SourcePath={SourcePath} BlockId={BlockId}", NotePath, e.SourcePath, e.BlockId);
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
            logger.LogWarning(ex, "Mention preview request failed. NotePath={NotePath} RelativePath={RelativePath}", NotePath, e.RelativePath);
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
            logger.LogError(ex, "Open mention request failed. NotePath={NotePath} SourcePath={SourcePath} RelativePath={RelativePath}", NotePath, e.SourcePath, e.RelativePath);
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
            logger.LogError(ex, "Create linked note request failed. NotePath={NotePath} SourcePath={SourcePath}", NotePath, e.SourcePath);
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
            KnowledgeAttachmentReadModel attachment = await viewModel.AttachAttachmentStreamToNoteAsync(
                e.SourcePath,
                e.FileName,
                stream);

            e.Complete(AttachFileResult.Success(
                attachment.OriginalFileName,
                attachment.RelativePath,
                attachment.Kind.ToString()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Editor attachment request failed. NotePath={NotePath} FileName={FileName}", NotePath, e.FileName);
            e.Complete(AttachFileResult.Fail(ex.Message));
        }
    }

    private async Task EditorView_AttachImageSourceRequested(object? sender, AttachImageSourceRequestedEventArgs e)
    {
        if (isLoadingEditorNote)
        {
            e.Complete(AttachFileResult.Fail("The note is still loading."));
            return;
        }

        try
        {
            Activate(selectInTree: false);
            PastedImageSourceContent sourceContent = await ReadPastedImageSourceAsync(
                e.ImageSource,
                e.FallbackFileName);
            await using MemoryStream stream = new(sourceContent.Content, writable: false);
            KnowledgeAttachmentReadModel attachment = await viewModel.AttachAttachmentStreamToNoteAsync(
                e.SourcePath,
                sourceContent.FileName,
                stream);

            e.Complete(AttachFileResult.Success(
                attachment.OriginalFileName,
                attachment.RelativePath,
                attachment.Kind.ToString()));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Editor image source attachment request failed. NotePath={NotePath} ImageSource={ImageSource}",
                NotePath,
                e.ImageSource);
            e.Complete(AttachFileResult.Fail(ex.Message));
        }
    }

    private async Task EditorView_ReadAttachmentRequested(object? sender, ReadAttachmentRequestedEventArgs e)
    {
        try
        {
            ReadAttachmentResult result = await viewModel.ReadImageAttachmentForCurrentNoteAsync(
                e.SourcePath,
                e.ImageSource);
            e.Complete(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Editor attachment read failed. NotePath={NotePath} SourcePath={SourcePath} ImageSource={ImageSource}", NotePath, e.SourcePath, e.ImageSource);
            e.Complete(ReadAttachmentResult.Fail(ex.Message));
        }
    }

    private async Task EditorView_StoreEditorBlobRequested(object? sender, StoreEditorBlobRequestedEventArgs e)
    {
        try
        {
            await viewModel.StoreEditorBlobAsync(
                e.SourcePath,
                e.BlobId,
                e.ContentType,
                e.ContentBase64);
            e.Complete(EditorBlobResult.Success());
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Editor blob store failed. NotePath={NotePath} SourcePath={SourcePath} BlobId={BlobId}",
                NotePath,
                e.SourcePath,
                e.BlobId);
            e.Complete(EditorBlobResult.Fail(ex.Message));
        }
    }

    private async Task EditorView_ReadEditorBlobRequested(object? sender, ReadEditorBlobRequestedEventArgs e)
    {
        try
        {
            EditorBlobReadResult result = await viewModel.ReadEditorBlobAsync(e.SourcePath, e.BlobId);
            e.Complete(result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Editor blob read failed. NotePath={NotePath} SourcePath={SourcePath} BlobId={BlobId}",
                NotePath,
                e.SourcePath,
                e.BlobId);
            e.Complete(EditorBlobReadResult.Fail(ex.Message));
        }
    }

    private Task EditorView_ListEditorBlobsRequested(object? sender, ListEditorBlobsRequestedEventArgs e)
    {
        try
        {
            IReadOnlyList<string> blobIds = viewModel.ListEditorBlobIds(e.SourcePath);
            e.Complete(EditorBlobListResult.Success(blobIds));
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Editor blob list failed. NotePath={NotePath} SourcePath={SourcePath}",
                NotePath,
                e.SourcePath);
            e.Complete(EditorBlobListResult.Fail(ex.Message));
        }

        return Task.CompletedTask;
    }

    private static async Task<PastedImageSourceContent> ReadPastedImageSourceAsync(
        string imageSource,
        string fallbackFileName)
    {
        string source = imageSource.Trim();
        if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            PastedImageSourceContent? dataContent = TryReadDataImageSource(source, fallbackFileName);
            return dataContent ?? throw new InvalidOperationException("The pasted data image source is invalid.");
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out Uri? uri))
        {
            return uri.Scheme.ToLowerInvariant() switch
            {
                "http" or "https" => await DownloadPastedHttpImageAsync(uri, fallbackFileName),
                "file" => await ReadPastedLocalImageAsync(uri.LocalPath, fallbackFileName, source),
                _ => throw new InvalidOperationException("The pasted image source scheme is not supported.")
            };
        }

        return await ReadPastedLocalImageAsync(source, fallbackFileName, source);
    }

    private static PastedImageSourceContent? TryReadDataImageSource(string source, string fallbackFileName)
    {
        int commaIndex = source.IndexOf(',');
        if (commaIndex < 0)
        {
            return null;
        }

        string metadata = source[5..commaIndex];
        string payload = source[(commaIndex + 1)..];
        string mediaType = metadata.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "text/plain";
        if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        byte[] content = metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase)
            ? Convert.FromBase64String(payload)
            : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
        if (content.Length == 0)
        {
            return null;
        }

        string fileName = CreatePastedImageFileName(fallbackFileName, source, mediaType);
        return new PastedImageSourceContent(fileName, content);
    }

    private static async Task<PastedImageSourceContent> DownloadPastedHttpImageAsync(
        Uri uri,
        string fallbackFileName)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Cognt", "1.0"));
        using HttpResponseMessage response = await PastedImageHttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (!IsSupportedPastedImageContentType(contentType)
            && !IsSupportedPastedImagePath(uri.LocalPath))
        {
            throw new InvalidOperationException("The pasted image URL did not return a supported image type.");
        }

        byte[] content = await response.Content.ReadAsByteArrayAsync();
        if (content.Length == 0)
        {
            throw new InvalidOperationException("The pasted image URL returned empty content.");
        }

        string fileName = CreatePastedImageFileName(
            GetPreferredPastedImageFileName(fallbackFileName, uri.LocalPath),
            uri.LocalPath,
            contentType);
        return new PastedImageSourceContent(fileName, content);
    }

    private static async Task<PastedImageSourceContent> ReadPastedLocalImageAsync(
        string path,
        string fallbackFileName,
        string source)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The pasted image file could not be found.", fullPath);
        }

        if (!IsSupportedPastedImagePath(fullPath))
        {
            throw new InvalidOperationException("The pasted image file type is not supported.");
        }

        byte[] content = await File.ReadAllBytesAsync(fullPath);
        if (content.Length == 0)
        {
            throw new InvalidOperationException("The pasted image file is empty.");
        }

        string fileName = CreatePastedImageFileName(
            GetPreferredPastedImageFileName(fallbackFileName, fullPath),
            source,
            string.Empty);
        return new PastedImageSourceContent(fileName, content);
    }

    private static string GetPreferredPastedImageFileName(string fallbackFileName, string sourcePath)
    {
        string sourceFileName = Path.GetFileName(sourcePath);
        return string.IsNullOrWhiteSpace(sourceFileName)
            ? fallbackFileName
            : sourceFileName;
    }

    private static string CreatePastedImageFileName(
        string preferredFileName,
        string source,
        string contentType)
    {
        string fileName = SanitizePastedImageFileName(preferredFileName);
        if (!string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            return fileName;
        }

        return fileName + InferPastedImageExtension(contentType, source);
    }

    private static string SanitizePastedImageFileName(string value)
    {
        string fileName = Path.GetFileName(value.Trim());
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "pasted-image";
        }

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '-');
        }

        return string.IsNullOrWhiteSpace(fileName)
            ? "pasted-image"
            : fileName;
    }

    private static string InferPastedImageExtension(string contentType, string source)
    {
        string normalizedContentType = contentType.Trim().ToLowerInvariant();
        return normalizedContentType switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/png" => ".png",
            _ => Path.GetExtension(source).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => ".jpg",
                ".webp" => ".webp",
                ".gif" => ".gif",
                _ => ".png"
            }
        };
    }

    private static bool IsSupportedPastedImageContentType(string contentType)
    {
        return contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedPastedImagePath(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif";
    }

    private sealed record PastedImageSourceContent(string FileName, byte[] Content);

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
            logger.LogError(ex, "Image occlusion editor failed. NotePath={NotePath} ImageBlockId={ImageBlockId} ImageRelPath={ImageRelPath}", NotePath, imageBlockId, imageRelPath);
            dialogs.ShowWarning("Image Occlusion", ex.Message);
        }
    }
}
