using System.IO;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    public void LoadCollection(string directoryPath, bool clearEditor = true)
    {
        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(directoryPath.Trim()));
        }
        catch (Exception ex)
        {
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
        OnPropertyChanged(nameof(OpenCollectionPath));
        OnPropertyChanged(nameof(CurrentCollectionPath));
        OnPropertyChanged(nameof(CurrentCollectionName));
        RefreshAvailableCollections();
        ClearCurrentNoteState();
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
        => RefreshAvailableCollections();

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

        if (Directory.Exists(collectionPath) || File.Exists(collectionPath))
        {
            throw new InvalidOperationException($"A collection named \"{FormatCollectionName(collectionPath)}\" already exists.");
        }

        Directory.CreateDirectory(collectionPath);
        LoadCollection(collectionPath);
    }

    private void RemoveCurrentCollection()
    {
        Navigation.CollectionRemovalPlan plan = GetCurrentCollectionRemovalPlan();
        if (!plan.CanRemove)
        {
            throw new InvalidOperationException(plan.Message);
        }

        string deletedPath = Path.GetFullPath(CurrentCollectionPath);
        string? nextCollectionPath = AvailableCollections
            .Select(collection => collection.Path)
            .FirstOrDefault(path => !AreSamePath(path, deletedPath) && Directory.Exists(path));

        SaveCurrentNoteFromEditor();
        noteService.CloseCurrentCollection();
        Directory.Delete(deletedPath, recursive: true);

        if (!string.IsNullOrWhiteSpace(nextCollectionPath))
        {
            LoadCollection(nextCollectionPath);
            return;
        }

        ClearOpenCollectionState();
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        ClearEditorRequested?.Invoke(this, EventArgs.Empty);
    }
}
