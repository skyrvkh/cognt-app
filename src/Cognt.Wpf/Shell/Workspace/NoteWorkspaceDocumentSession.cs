using System.IO;
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

namespace CogntApp.Shell.Workspace;

internal sealed class NoteWorkspaceDocumentSession
{
    private readonly MainViewModel viewModel;
    private readonly IEditorViewFactory editorViewFactory;
    private readonly ContentControl dockedHost = new();
    private readonly IShellDialogService dialogs;
    private readonly IImageMaskEditorDialogService imageMaskEditorDialogs;
    private readonly Func<string, bool, Task> openNoteAsync;
    private readonly Action mentionTargetsChanged;

    private EditorView? editorView;
    private ContentControl? activeHost;
    private DetachedNoteWindow? detachedWindow;
    private string currentContent;
    private NoteWorkspaceDocumentState state = NoteWorkspaceDocumentState.Docked;
    private bool isInitialized;
    private bool isMountingEditor;
    private bool isClosingDetachedWindowForRedock;
    private bool currentReadOnlyState;
    private int documentLinksRefreshVersion;

    internal NoteWorkspaceDocumentSession(
        MainViewModel viewModel,
        IEditorViewFactory editorViewFactory,
        IShellDialogService dialogs,
        IImageMaskEditorDialogService imageMaskEditorDialogs,
        Func<string, bool, Task> openNoteAsync,
        Action mentionTargetsChanged,
        PreparedNote note)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.editorViewFactory = editorViewFactory ?? throw new ArgumentNullException(nameof(editorViewFactory));
        this.dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        this.imageMaskEditorDialogs = imageMaskEditorDialogs ?? throw new ArgumentNullException(nameof(imageMaskEditorDialogs));
        this.openNoteAsync = openNoteAsync ?? throw new ArgumentNullException(nameof(openNoteAsync));
        this.mentionTargetsChanged = mentionTargetsChanged ?? throw new ArgumentNullException(nameof(mentionTargetsChanged));
        ArgumentNullException.ThrowIfNull(note);

        NotePath = note.NotePath;
        ItemId = note.ItemId;
        Title = note.Title;
        currentContent = note.Content;

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

    internal bool IsHostReady => editorView?.IsHostReady == true;

    internal string GetContent() => editorView?.GetContent() ?? currentContent;

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

    internal async Task CloseAsync()
    {
        if (IsClosed)
        {
            return;
        }

        state = NoteWorkspaceDocumentState.Closed;
        currentContent = GetContent();
        UnmountEditor();
        CloseDetachedWindowForRedock();
        dockedHost.Content = null;
        await Task.CompletedTask;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    internal bool Activate(bool selectInTree)
    {
        viewModel.GetCurrentEditorContent = GetContent;
        if (string.Equals(viewModel.CurrentNotePath, NotePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        viewModel.SetActiveWorkspaceNote(NotePath, Title, GetContent(), ItemId);

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
        LayoutDocument.Closed -= LayoutDocument_Closed;
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

        await CloseAsync();
    }

    private async void DetachedWindow_Closed(object? sender, EventArgs e)
    {
        if (isClosingDetachedWindowForRedock || IsClosed)
        {
            return;
        }

        detachedWindow = null;
        await CloseAsync();
    }

    private async Task MountEditorIntoHostAsync(ContentControl host)
    {
        if (!isInitialized || IsClosed || isMountingEditor)
        {
            return;
        }

        isMountingEditor = true;

        try
        {
            EditorView nextEditor = editorViewFactory.Create();
            WireEditorView(nextEditor);
            editorView = nextEditor;
            activeHost = host;
            host.Content = nextEditor;

            await WaitForEditorLoadedAsync(nextEditor);
            await nextEditor.LoadNoteAsync(
                Title,
                currentContent,
                NotePath,
                viewModel.OpenCollectionPath);

            if (currentReadOnlyState)
            {
                await nextEditor.SetReadOnlyAsync(true);
            }

            await RefreshMentionTargetsAsync();
            RefreshDocumentLinks();
        }
        finally
        {
            isMountingEditor = false;
        }
    }

    private void UnmountEditor()
    {
        EditorView? existingEditor = editorView;
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

    private static Task WaitForEditorLoadedAsync(EditorView nextEditor)
    {
        if (nextEditor.IsLoaded)
        {
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnLoaded(object? sender, RoutedEventArgs e)
        {
            nextEditor.Loaded -= OnLoaded;
            completion.TrySetResult();
        }

        nextEditor.Loaded += OnLoaded;
        return completion.Task;
    }

    private void WireEditorView(EditorView nextEditor)
    {
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

    private void EditorView_ContentChanged(object? sender, string content)
    {
        currentContent = content;

        try
        {
            Activate(selectInTree: false);
            viewModel.SaveNoteIfChanged(content);
            RefreshDocumentLinks();
        }
        catch (Exception ex)
        {
            editorView?.PostSaveResult(ok: false, error: ex.Message);
        }
    }

    private async void EditorView_CreateExtractRequested(object? sender, CreateExtractRequestedEventArgs e)
    {
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
        currentContent = e.Content;
        await OpenImageOcclusionForImageAsync(e.ImageBlockId, e.ImageRelPath, e.Content);
    }

    private void EditorView_MentionSearchRequested(object? sender, MentionSearchRequestedEventArgs e)
    {
        e.Complete(viewModel.SearchMentionTargets(e.Query, e.Limit));
    }

    private void EditorView_MentionPreviewRequested(object? sender, MentionPreviewRequestedEventArgs e)
    {
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
        currentContent = e.Content;

        try
        {
            Activate(selectInTree: false);
            viewModel.SaveMentionSource(e.SourcePath, e.Content);
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
            ImageMaskEditorResult result = await imageMaskEditorDialogs.ShowAsync(
                request,
                async applied =>
                {
                    viewModel.SaveImageMaskEditorResult(applied, content);
                    editorView?.PostSaveResult(ok: true);
                    await Task.CompletedTask;
                });
            if (!result.Saved)
            {
                return;
            }

            viewModel.SaveImageMaskEditorResult(result, content);
            editorView?.PostSaveResult(ok: true);
        }
        catch (Exception ex)
        {
            dialogs.ShowWarning("Image Occlusion", ex.Message);
        }
    }
}
