using System.IO;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public void LoadCollection(string directoryPath, bool clearEditor = true)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            RaiseMessage("Invalid Collection", "The collection path is required.");
            return;
        }

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(directoryPath.Trim()));
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Collection path normalization failed. DirectoryPath={DirectoryPath}", directoryPath);
            RaiseMessage("Invalid Collection", $"The collection path is invalid: {ex.Message}");
            return;
        }

        if (!collectionPathResolver.IsCollectionPath(fullPath))
        {
            RaiseMessage(
                "Invalid Collection",
                $"Only collection folders ending with \"{collectionPathResolver.CollectionDirectoryExtension}\" can be opened.");
            return;
        }

        if (!Directory.Exists(fullPath))
        {
            RaiseMessage("Collection Not Found", $"The collection folder does not exist:{Environment.NewLine}{fullPath}");
            return;
        }

        bool isSwitchingCollection =
            !string.IsNullOrWhiteSpace(openCollectionPath)
            && !string.Equals(openCollectionPath, fullPath, StringComparison.OrdinalIgnoreCase);

        if (isSwitchingCollection)
        {
            SaveCurrentNoteFromEditor();
            noteService.CloseCurrentCollection();
        }

        openCollectionPath = fullPath;
        CollectionPath = openCollectionPath;
        knownCollectionStore.RememberCollectionPath(openCollectionPath);
        OnPropertyChanged(nameof(OpenCollectionPath));
        OnPropertyChanged(nameof(CurrentCollectionPath));
        OnPropertyChanged(nameof(CurrentCollectionName));
        RefreshAvailableCollections();
        ClearCurrentNoteState();
        RefreshEditorSettings();
        RefreshNotesTree(preserveExpansion: true);
        RefreshReviewSettings();
        RefreshGraphSettings();
        RefreshDashboardData();
        ReloadNoteToCardDeckNames();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);

        if (clearEditor)
        {
            ClearEditorRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SelectCollection(object? parameter)
    {
        if (parameter is not Navigation.CollectionOptionViewModel collection
            || string.IsNullOrWhiteSpace(collection.Path)
            || AreSamePath(collection.Path, CurrentCollectionPath))
        {
            return;
        }

        LoadCollection(collection.Path);
    }

    public void RefreshCollectionSummaries()
    {
        RefreshAvailableCollections();
    }

    private void AddCollection(object? parameter)
    {
        string rawName = parameter switch
        {
            Navigation.CollectionCreationRequest request => request.Name,
            string name => name,
            _ => string.Empty
        };
        string parentPath = parameter is Navigation.CollectionCreationRequest creationRequest
            ? Path.GetFullPath(Environment.ExpandEnvironmentVariables(creationRequest.ParentPath.Trim()))
            : ResolveCollectionCreationParentPath();

        if (!Directory.Exists(parentPath))
        {
            throw new DirectoryNotFoundException($"The collection location does not exist: {parentPath}");
        }

        string collectionDirectoryName = NormalizeCollectionDirectoryName(rawName);
        string collectionPath = Path.Combine(parentPath, collectionDirectoryName);

        if (Directory.Exists(collectionPath))
        {
            LoadCollection(collectionPath);
            return;
        }

        if (File.Exists(collectionPath))
        {
            throw new InvalidOperationException($"A file named \"{Path.GetFileName(collectionPath)}\" already exists.");
        }

        Directory.CreateDirectory(collectionPath);

        LoadCollection(collectionPath);
    }

    private void RemoveCollectionFromList(object? parameter)
    {
        Navigation.CollectionRemovalPlan plan = GetCollectionListRemovalPlan(parameter as Navigation.CollectionOptionViewModel);
        if (!plan.CanRemove)
        {
            throw new InvalidOperationException(plan.Message);
        }

        if (!TryNormalizeCollectionActionPath(parameter, out string removedPath))
        {
            throw new InvalidOperationException("Choose a collection to remove from the list.");
        }

        bool isCurrentCollection = AreSamePath(removedPath, CurrentCollectionPath);
        string? nextCollectionPath = isCurrentCollection
            ? FindNextAvailableCollectionPath(removedPath)
            : null;

        if (isCurrentCollection)
        {
            SaveCurrentNoteFromEditor();
            noteService.CloseCurrentCollection();
        }

        knownCollectionStore.HideCollectionPath(removedPath);
        logger?.LogInformation(
            "Removed collection from selector list. RemovedPath={RemovedPath} NextCollectionPath={NextCollectionPath}",
            removedPath,
            nextCollectionPath);

        if (isCurrentCollection)
        {
            OpenNextCollectionOrClear(nextCollectionPath);
            return;
        }

        RefreshAvailableCollections();
    }

    private void DeleteCollectionFromDisk(object? parameter)
    {
        Navigation.CollectionRemovalPlan plan = GetCollectionDeletionPlan(parameter as Navigation.CollectionOptionViewModel);
        if (!plan.CanRemove)
        {
            throw new InvalidOperationException(plan.Message);
        }

        if (!TryNormalizeCollectionActionPath(parameter, out string deletedPath)
            || !Directory.Exists(deletedPath))
        {
            throw new InvalidOperationException("Choose an existing collection to delete.");
        }

        bool isCurrentCollection = AreSamePath(deletedPath, CurrentCollectionPath);
        string? nextCollectionPath = isCurrentCollection
            ? FindNextAvailableCollectionPath(deletedPath)
            : null;

        if (isCurrentCollection)
        {
            SaveCurrentNoteFromEditor();
            noteService.CloseCurrentCollection();
        }

        knownCollectionStore.ForgetCollectionPath(deletedPath);
        knownCollectionStore.UnhideCollectionPath(deletedPath);
        Directory.Delete(deletedPath, recursive: true);
        logger?.LogInformation(
            "Deleted collection from disk. DeletedPath={DeletedPath} NextCollectionPath={NextCollectionPath}",
            deletedPath,
            nextCollectionPath);

        if (isCurrentCollection)
        {
            OpenNextCollectionOrClear(nextCollectionPath);
            return;
        }

        RefreshAvailableCollections();
    }

    private void OpenNextCollectionOrClear(string? nextCollectionPath)
    {
        if (!string.IsNullOrWhiteSpace(nextCollectionPath) && Directory.Exists(nextCollectionPath))
        {
            LoadCollection(nextCollectionPath);
            return;
        }

        ClearOpenCollectionState();
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        ClearEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    private string? FindNextAvailableCollectionPath(string deletedPath)
    {
        foreach (Navigation.CollectionOptionViewModel collection in AvailableCollections)
        {
            string path = collection.Path;
            if (!AreSamePath(path, deletedPath) && Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
