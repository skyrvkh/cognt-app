using System.Windows;
using System.Windows.Controls;
using AvalonDock.Layout;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Wpf.EditorHost;
using Cognt.Wpf.EditorHost.Models.Editor;
using Cognt.Wpf.EditorHost.Views;
using CogntApp.ViewModels.Decks.PresentationModels;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Workspace;

internal sealed class FlashcardWorkspaceDocumentSession
{
    private static readonly TimeSpan EditorViewLoadTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan AutoSaveDelay = TimeSpan.FromMilliseconds(750);

    private readonly IEditorViewFactory editorViewFactory;
    private readonly IUpdateFlashcardContentCommand updateFlashcardContentCommand;
    private readonly string collectionId;
    private readonly CardLibraryRow card;
    private readonly Func<EditorTheme> getEditorTheme;
    private readonly ILogger<FlashcardWorkspaceDocumentSession> logger;
    private readonly ContentControl dockedHost = new();
    private readonly string editorPath;
    private readonly bool isReadOnly;

    private EditorView? editorView;
    private string currentContent;
    private string lastPersistedContent;
    private bool isInitialized;
    private bool isLoadingCard;
    private bool isMountingEditor;
    private bool isClosed;
    private bool hasPendingContentChanges;
    private int autoSaveVersion;
    private CancellationTokenSource? scheduledAutoSaveCancellation;

    internal FlashcardWorkspaceDocumentSession(
        IEditorViewFactory editorViewFactory,
        IUpdateFlashcardContentCommand updateFlashcardContentCommand,
        string collectionId,
        CardLibraryRow card,
        Func<EditorTheme> getEditorTheme,
        ILogger<FlashcardWorkspaceDocumentSession> logger,
        string? fallbackTitle = null)
    {
        this.editorViewFactory = editorViewFactory ?? throw new ArgumentNullException(nameof(editorViewFactory));
        this.updateFlashcardContentCommand = updateFlashcardContentCommand ?? throw new ArgumentNullException(nameof(updateFlashcardContentCommand));
        this.collectionId = string.IsNullOrWhiteSpace(collectionId)
            ? throw new ArgumentException("Collection ID is required.", nameof(collectionId))
            : collectionId;
        this.card = card ?? throw new ArgumentNullException(nameof(card));
        this.getEditorTheme = getEditorTheme ?? throw new ArgumentNullException(nameof(getEditorTheme));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        CardId = card.Id;
        currentContent = card.FrontContent ?? string.Empty;
        lastPersistedContent = currentContent;
        isReadOnly = card.Id <= 0 || !card.IsEditableCardType;
        Title = CreateTitle(card, fallbackTitle);
        editorPath = $"flashcards/card-{CardId}.json";

        LayoutDocument = new LayoutDocument
        {
            Title = Title,
            ContentId = $"flashcard:{CardId}",
            CanClose = true,
            CanFloat = false,
            Content = dockedHost,
        };
        LayoutDocument.Closed += LayoutDocument_Closed;
    }

    internal event EventHandler? Closed;

    internal long CardId { get; }

    internal string Title { get; }

    internal LayoutDocument LayoutDocument { get; }

    internal bool IsClosed => isClosed;

    internal bool HasMountedEditor => editorView is not null
        && ReferenceEquals(dockedHost.Content, editorView);

    internal async Task InitializeAsync()
    {
        if (isClosed)
        {
            return;
        }

        isInitialized = true;
        await MountEditorIntoHostAsync();
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

    internal void Focus()
    {
        if (!LayoutDocument.IsSelected)
        {
            LayoutDocument.IsSelected = true;
        }

        if (!LayoutDocument.IsActive)
        {
            LayoutDocument.IsActive = true;
        }
    }

    internal async Task CreateClozeFromSelectionAsync()
    {
        EditorView? activeEditor = editorView;
        if (activeEditor is null)
        {
            return;
        }

        Focus();
        if (isReadOnly)
        {
            activeEditor.PostSaveResult(
                ok: false,
                error: "This card type is read-only in the navigation editor.");
            return;
        }

        await activeEditor.CreateClozeFromSelectionAsync();
    }

    internal Task SavePendingChangesAsync()
    {
        return SavePendingChangesAsync(postResult: false, reason: "close");
    }

    internal async Task CloseAsync(bool savePendingChanges = true)
    {
        if (isClosed)
        {
            return;
        }

        if (savePendingChanges)
        {
            await SavePendingChangesAsync();
        }

        isClosed = true;
        currentContent = editorView?.GetContent() ?? currentContent;
        CancelScheduledAutoSave();
        UnmountEditor();
        dockedHost.Content = null;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    internal void Dispose()
    {
        LayoutDocument.Closed -= LayoutDocument_Closed;
        CancelScheduledAutoSave();
        UnmountEditor();
        dockedHost.Content = null;
    }

    private async Task MountEditorIntoHostAsync()
    {
        if (!isInitialized || isClosed || isMountingEditor)
        {
            return;
        }

        isMountingEditor = true;
        try
        {
            EditorView nextEditor = editorViewFactory.Create();
            nextEditor.ApplyTheme(getEditorTheme());
            WireEditorView(nextEditor);
            editorView = nextEditor;
            dockedHost.Content = nextEditor;

            await WorkspaceEditorViewLoadAwaiter.WaitAsync(nextEditor, EditorViewLoadTimeout, logger);
            isLoadingCard = true;
            await nextEditor.LoadNoteAsync(
                Title,
                currentContent,
                editorPath,
                collectionId);
            currentContent = nextEditor.GetContent();
            lastPersistedContent = currentContent;
            hasPendingContentChanges = false;

            if (isReadOnly)
            {
                await nextEditor.SetReadOnlyAsync(true);
                nextEditor.PostSaveResult(
                    ok: false,
                    error: "This card type is read-only in the navigation editor.");
            }
        }
        catch
        {
            UnwireEditorView(editorView);
            editorView?.DisposeEditorHost();
            editorView = null;
            dockedHost.Content = null;
            throw;
        }
        finally
        {
            isLoadingCard = false;
            isMountingEditor = false;
        }
    }

    private void UnmountEditor()
    {
        EditorView? existingEditor = editorView;
        isLoadingCard = false;
        currentContent = existingEditor?.GetContent() ?? currentContent;
        UnwireEditorView(existingEditor);
        dockedHost.Content = null;
        existingEditor?.DisposeEditorHost();
        editorView = null;
    }

    private void WireEditorView(EditorView nextEditor)
    {
        nextEditor.ContentChanged += EditorView_ContentChanged;
        nextEditor.DocumentChanged += EditorView_DocumentChanged;
        nextEditor.SaveClozeRequested += EditorView_SaveClozeRequested;
    }

    private void UnwireEditorView(EditorView? existingEditor)
    {
        if (existingEditor is null)
        {
            return;
        }

        existingEditor.ContentChanged -= EditorView_ContentChanged;
        existingEditor.DocumentChanged -= EditorView_DocumentChanged;
        existingEditor.SaveClozeRequested -= EditorView_SaveClozeRequested;
    }

    private void EditorView_ContentChanged(object? sender, string content)
    {
        if (isLoadingCard)
        {
            currentContent = content;
            return;
        }

        currentContent = content;
        MarkContentChanged();
    }

    private void EditorView_DocumentChanged(object? sender, EventArgs e)
    {
        if (!isLoadingCard)
        {
            MarkContentChanged();
        }
    }

    private async void EditorView_SaveClozeRequested(object? sender, SaveClozeRequestedEventArgs e)
    {
        if (isLoadingCard)
        {
            return;
        }

        currentContent = e.Content;
        hasPendingContentChanges = !string.Equals(currentContent, lastPersistedContent, StringComparison.Ordinal);

        try
        {
            await SavePendingChangesAsync(postResult: true, reason: "cloze");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Flashcard cloze save failed. CardId={CardId}", CardId);
            editorView?.PostSaveResult(ok: false, error: ex.Message);
        }
    }

    private void MarkContentChanged()
    {
        if (isReadOnly)
        {
            return;
        }

        hasPendingContentChanges = !string.Equals(currentContent, lastPersistedContent, StringComparison.Ordinal);
        ScheduleAutoSave();
    }

    private void ScheduleAutoSave()
    {
        CancelScheduledAutoSave();
        if (isClosed || !HasPendingContentChanges())
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
                    await Task.Delay(AutoSaveDelay, cancellationToken);
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
                await SavePendingChangesAsync(postResult: true, reason: "auto-save");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Flashcard auto-save failed. CardId={CardId}", CardId);
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
        if (isReadOnly)
        {
            return;
        }

        currentContent = await GetLatestContentAsync();
        if (!HasPendingContentChanges())
        {
            return;
        }

        string contentToSave = currentContent;
        try
        {
            await updateFlashcardContentCommand.ExecuteAsync(
                new UpdateFlashcardContentRequest(
                    collectionId,
                    CardId,
                    contentToSave,
                    card.IsBasicCard ? card.BackContent : null,
                    card.Deck,
                    card.Tags));
            lastPersistedContent = contentToSave;
            hasPendingContentChanges = false;

            if (postResult)
            {
                editorView?.PostSaveResult(ok: true);
            }
        }
        catch
        {
            if (postResult)
            {
                editorView?.PostSaveResult(ok: false, error: "The card could not be saved.");
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

    private async void LayoutDocument_Closed(object? sender, EventArgs e)
    {
        try
        {
            await CloseAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Flashcard close failed while saving. CardId={CardId}", CardId);
            editorView?.PostSaveResult(ok: false, error: ex.Message);
        }
    }

    private static string CreateTitle(CardLibraryRow card, string? fallbackTitle)
    {
        string title = string.IsNullOrWhiteSpace(card.Question)
            ? fallbackTitle ?? $"Card {card.Id}"
            : card.Question;

        title = title.ReplaceLineEndings(" ").Trim();
        if (title.StartsWith("[Card]", StringComparison.OrdinalIgnoreCase))
        {
            title = title[6..].Trim();
        }

        if (title.Length > 60)
        {
            title = title[..57] + "...";
        }

        return string.IsNullOrWhiteSpace(title) ? $"Card {card.Id}" : title;
    }
}
