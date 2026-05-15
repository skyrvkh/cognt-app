using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Navigation = CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public void Initialize()
    {
        string defaultCollectionPath = collectionPathResolver.ResolveDefaultCollectionPath();
        EnsureDefaultCollectionExists(defaultCollectionPath);
        CollectionPath = defaultCollectionPath;
        RefreshAvailableCollections();

        if (Directory.Exists(defaultCollectionPath) && collectionPathResolver.IsCollectionPath(defaultCollectionPath))
        {
            LoadCollection(defaultCollectionPath, clearEditor: false);
        }

        ShowDashboard();
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
        OnPropertyChanged(nameof(ShouldShowNoNotesState));
    }

    private void ClearOpenCollectionState()
    {
        openCollectionPath = string.Empty;
        CollectionPath = string.Empty;
        currentCollectionNoteCount = 0;
        currentCollectionCardCount = 0;
        NoteRoots.Clear();
        RecentNotes.Clear();
        DashboardMetrics.Clear();
        DashboardStudyHeatmapCells.Clear();
        NoteToCardDeckNames.Clear();
        ApplyReviewSettings(FlashcardReviewSettingsReadModel.Default);
        ApplyGraphSettings(KnowledgeGraphSettingsReadModel.Default);
        ReviewSettingsStatusMessage = string.Empty;
        GraphSettingsStatusMessage = string.Empty;
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
        noteService.Dispose();
    }
}
