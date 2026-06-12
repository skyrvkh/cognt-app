using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.Design;
using Microsoft.Extensions.Logging;
using Navigation = CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public void Initialize()
    {
        ClearOpenCollectionState();
        RefreshAvailableCollections();
        OpenRecentCollection();

        ShowDashboard();
    }

    private void OpenRecentCollection()
    {
        string? recentCollectionPath = knownCollectionStore.LoadRecentCollectionPath();
        if (string.IsNullOrWhiteSpace(recentCollectionPath))
        {
            return;
        }

        if (TryNormalizeExistingCollectionPath(recentCollectionPath, out string fullPath))
        {
            LoadCollection(fullPath, clearEditor: false);
            return;
        }

        logger?.LogWarning("Recent collection path is invalid and will be forgotten. RecentCollectionPath={RecentCollectionPath}", recentCollectionPath);
        knownCollectionStore.ForgetCollectionPath(recentCollectionPath);
    }

    private void ClearCurrentNoteState(bool clearPdfDocument = true)
    {
        currentNotePath = null;
        currentNoteItemId = null;
        currentNoteTitle = string.Empty;
        lastSavedContent = string.Empty;
        if (clearPdfDocument)
        {
            currentPdfDocument = null;
        }

        CurrentNoteToCardQueue = null;
        ClearNoteToCardTags();
        EditorBreadcrumbItems.Clear();
        OnPropertyChanged(nameof(CurrentNotePath));
        OnPropertyChanged(nameof(CurrentNoteRelativePath));
        OnPropertyChanged(nameof(ShouldShowNoNotesState));
        CurrentNoteContentChanged?.Invoke(this, string.Empty);
    }

    private void ClearOpenCollectionState()
    {
        openCollectionPath = string.Empty;
        CollectionPath = string.Empty;
        currentCollectionNoteCount = 0;
        currentCollectionCardCount = 0;
        NoteRoots.Clear();
        ResetLoadedTreeNodeIndex();
        transientNoteToCardDrafts.Clear();
        RecentNotes.Clear();
        DashboardMetrics.Clear();
        DashboardStudyHeatmapCells.Clear();
        NoteToCardDeckNames.Clear();
        FilteredNoteToCardDeckNames.Clear();
        noteToCardDeckNameCatalog = [];
        noteToCardDeckNameSet.Clear();
        ApplyReviewSettings(FlashcardReviewSettingsReadModel.Default);
        ApplyGraphSettings(KnowledgeGraphSettingsReadModel.Default);
        ApplyEditorSettings(EditorSettings.Default, save: false, raiseChanged: true);
        ReviewSettingsStatusMessage = string.Empty;
        GraphSettingsStatusMessage = string.Empty;
        AutoSaveSettingsStatusMessage = string.Empty;
        ClearCurrentNoteState();
        OnPropertyChanged(nameof(OpenCollectionPath));
        OnPropertyChanged(nameof(CurrentCollectionPath));
        OnPropertyChanged(nameof(CurrentCollectionName));
        NotifyCollectionStateChanged();
    }

    private void RaiseMessage(string title, string message)
    {
        UserMessageRequested?.Invoke(this, new Navigation.UserMessageRequestedEventArgs(title, message));
    }

    private void RunUserAction(string title, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "User action failed. ErrorTitle={Title}", title);
            RaiseMessage(title, ex.Message);
        }
    }

    private static bool IsRecoverableFileSystemException(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or DirectoryNotFoundException
            or FileNotFoundException
            or PathTooLongException
            or NotSupportedException;
    }

    public void Dispose()
    {
        globalSearchCancellation?.Cancel();
        globalSearchCancellation?.Dispose();
        noteSaveSemaphore.Dispose();
        noteService.Dispose();
    }
}
