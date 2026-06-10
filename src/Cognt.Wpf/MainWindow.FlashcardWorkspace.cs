using System.Windows.Threading;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.Shell.Workspace;
using CogntApp.ViewModels.Decks.PresentationModels;
using CogntApp.ViewModels.Navigation;
using Microsoft.Extensions.Logging;

namespace CogntApp;

public partial class MainWindow
{
    private async Task OpenFlashcardDocumentAsync(OpenFlashcardRequestedEventArgs e)
    {
        overlayNavigation.HideOverlay();

        string collectionId = viewModel.OpenCollectionPath;
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            shellDialogs.ShowWarning("Open Card Failed", "Open a collection before opening a flashcard.");
            return;
        }

        if (openFlashcardDocuments.TryGetValue(e.CardId, out FlashcardWorkspaceDocumentSession? existingSession))
        {
            if (existingSession.IsClosed || !existingSession.HasMountedEditor)
            {
                logger.LogWarning(
                    "Discarding stale flashcard workspace session before reopening. CardId={CardId} IsClosed={IsClosed} HasMountedEditor={HasMountedEditor}",
                    e.CardId,
                    existingSession.IsClosed,
                    existingSession.HasMountedEditor);
                DiscardFlashcardDocumentSession(existingSession);
            }
            else
            {
                ActivateMainWorkspaceWindow();
                activeNoteDocument = null;
                existingSession.Focus();
                return;
            }
        }

        CardLibraryRow? card = LoadFlashcardRow(collectionId, e.CardId, e.SourceRelPath);
        if (card is null)
        {
            logger.LogWarning(
                "Open flashcard document failed because card row could not be loaded. CollectionId={CollectionId} CardId={CardId} SourceRelPath={SourceRelPath}",
                collectionId,
                e.CardId,
                e.SourceRelPath);
            shellDialogs.ShowWarning("Open Card Failed", "The selected flashcard could not be loaded.");
            return;
        }

        FlashcardWorkspaceDocumentSession session = new(
            editorViewFactory,
            updateFlashcardContentCommand,
            collectionId,
            card,
            CreateEditorTheme,
            loggerFactory.CreateLogger<FlashcardWorkspaceDocumentSession>(),
            e.Title);
        session.Closed += FlashcardDocumentSession_Closed;
        openFlashcardDocuments[e.CardId] = session;
        session.LayoutDocument.CanFloat = false;
        WorkspaceDocumentPane.Children.Add(session.LayoutDocument);
        session.LayoutDocument.IsSelected = true;
        session.LayoutDocument.IsActive = true;
        activeNoteDocument = null;
        ActivateMainWorkspaceWindow();
        WorkspaceDockingManager.UpdateLayout();

        try
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await session.InitializeAsync();
            session.Focus();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Flashcard document initialization failed. CardId={CardId}", e.CardId);
            DiscardFlashcardDocumentSession(session);
            shellDialogs.ShowWarning("Open Card Failed", $"The editor could not open this card: {ex.Message}");
        }
    }

    private CardLibraryRow? LoadFlashcardRow(
        string collectionId,
        long cardId,
        string? sourceRelPath)
    {
        if (!string.IsNullOrWhiteSpace(sourceRelPath))
        {
            CardLibraryRow? sourceCard = QueryFlashcardRows(
                    new FlashcardCardsQueryRequest(
                        collectionId,
                        SourceRelPath: sourceRelPath,
                        Limit: 10_000))
                .FirstOrDefault(row => row.Id == cardId);
            if (sourceCard is not null)
            {
                return sourceCard;
            }
        }

        CardLibraryRow? card = QueryFlashcardRows(
                new FlashcardCardsQueryRequest(
                    collectionId,
                    Limit: 10_000))
            .FirstOrDefault(row => row.Id == cardId);
        return card;
    }

    private IReadOnlyList<CardLibraryRow> QueryFlashcardRows(FlashcardCardsQueryRequest request)
    {
        IReadOnlyList<FlashcardCardSummaryReadModel> cards = flashcardCardsQuery.Execute(request);
        return CardsPresentationFactory.CreateCardLibraryRows(cards);
    }

    private void DiscardFlashcardDocumentSession(FlashcardWorkspaceDocumentSession session)
    {
        session.Closed -= FlashcardDocumentSession_Closed;
        openFlashcardDocuments.Remove(session.CardId);

        if (WorkspaceDocumentPane.Children.Contains(session.LayoutDocument))
        {
            WorkspaceDocumentPane.Children.Remove(session.LayoutDocument);
        }

        session.Dispose();
    }

    private void RemoveFlashcardDocumentSession(FlashcardWorkspaceDocumentSession session)
    {
        session.Closed -= FlashcardDocumentSession_Closed;
        openFlashcardDocuments.Remove(session.CardId);
        session.Dispose();
    }

    private void FlashcardDocumentSession_Closed(object? sender, EventArgs e)
    {
        if (sender is FlashcardWorkspaceDocumentSession session)
        {
            RemoveFlashcardDocumentSession(session);
        }
        else
        {
        }
    }
}
