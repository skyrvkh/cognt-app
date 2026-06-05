using System.IO;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    internal Navigation.CollectionRemovalPlan GetCurrentCollectionRemovalPlan()
        => GetCollectionDeletionPlan(null);

    internal Navigation.CollectionRemovalPlan GetCollectionListRemovalPlan(Navigation.CollectionOptionViewModel? collection)
    {
        if (!TryNormalizeCollectionActionPath(collection, out string collectionPath))
        {
            return Navigation.CollectionRemovalPlan.Blocked("Choose a collection to remove from the list.");
        }

        string collectionName = FormatCollectionName(collectionPath);
        string message = AreSamePath(collectionPath, CurrentCollectionPath)
            ? $"Remove \"{collectionName}\" from the collection list?{Environment.NewLine}{Environment.NewLine}The collection folder and files stay on disk. The open collection will be closed, and another available collection will open if one exists."
            : $"Remove \"{collectionName}\" from the collection list?{Environment.NewLine}{Environment.NewLine}The collection folder and files stay on disk. You can add it back later with Open existing.";

        return Navigation.CollectionRemovalPlan.Allowed(message);
    }

    internal Navigation.CollectionRemovalPlan GetCollectionDeletionPlan(Navigation.CollectionOptionViewModel? collection)
    {
        if (!TryNormalizeCollectionActionPath(collection, out string collectionPath)
            || !Directory.Exists(collectionPath))
        {
            return Navigation.CollectionRemovalPlan.Blocked("Choose an existing collection to delete.");
        }

        (int noteCount, int cardCount) = GetCollectionCounts(collectionPath);
        string collectionName = FormatCollectionName(collectionPath);

        return Navigation.CollectionRemovalPlan.Allowed(
            $"Delete collection \"{collectionName}\" from the hard drive?{Environment.NewLine}{Environment.NewLine}This permanently deletes the folder:{Environment.NewLine}{collectionPath}{Environment.NewLine}{Environment.NewLine}It contains {CountDisplayFormatter.Format(noteCount, "note")} and {CountDisplayFormatter.Format(cardCount, "card")}. This cannot be undone.");
    }

    private void RefreshAvailableCollections()
    {
        string selectedPath = CurrentCollectionPath;

        SortedSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

        AddCollectionPath(paths, selectedPath, includeHidden: true);
        AddCollectionPath(paths, CollectionPath);

        foreach (string knownCollectionPath in knownCollectionStore.LoadCollectionPaths())
        {
            if (IsExistingCollectionPath(knownCollectionPath))
            {
                AddCollectionPath(paths, knownCollectionPath);
            }
            else
            {
                logger?.LogWarning("Forgetting missing or invalid known collection path. KnownCollectionPath={KnownCollectionPath}", knownCollectionPath);
                knownCollectionStore.ForgetCollectionPath(knownCollectionPath);
            }
        }

        string parentPath = GetCollectionParentPath(selectedPath)
            ?? GetCollectionParentPath(CollectionPath)
            ?? GetCollectionParentPath(collectionPathResolver.ResolveDefaultCollectionPath())
            ?? string.Empty;

        if (Directory.Exists(parentPath))
        {
            try
            {
                foreach (string path in Directory.EnumerateDirectories(
                    parentPath,
                    $"*{collectionPathResolver.CollectionDirectoryExtension}",
                    SearchOption.TopDirectoryOnly))
                {
                    AddCollectionPath(paths, path);
                }
            }
            catch (IOException)
            {
                logger?.LogWarning("Could not enumerate collection parent due to IO error. ParentPath={ParentPath}", parentPath);
            }
            catch (UnauthorizedAccessException)
            {
                logger?.LogWarning("Could not enumerate collection parent due to unauthorized access. ParentPath={ParentPath}", parentPath);
            }
        }

        Dictionary<string, string> nextCollectionPathsById = new(StringComparer.OrdinalIgnoreCase);
        AvailableCollections.Clear();
        currentCollectionNoteCount = 0;
        currentCollectionCardCount = 0;

        foreach (string path in paths)
        {
            (int noteCount, int cardCount) = GetCollectionCounts(path);
            bool isSelected = AreSamePath(path, selectedPath);
            if (isSelected)
            {
                currentCollectionNoteCount = noteCount;
                currentCollectionCardCount = cardCount;
            }

            Navigation.CollectionOptionViewModel collection = new(
                path,
                FormatCollectionName(path),
                path,
                isSelected,
                noteCount,
                cardCount);
            AvailableCollections.Add(collection);
            nextCollectionPathsById[collection.Id] = collection.Path;
        }

        collectionPathsById = nextCollectionPathsById;

        NotifyCollectionStateChanged();
    }

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasAvailableCollections));
        OnPropertyChanged(nameof(ShouldShowNoCollectionsState));
        OnPropertyChanged(nameof(CurrentCollectionSummary));
        OnPropertyChanged(nameof(HasOpenCollection));
        OnPropertyChanged(nameof(ShouldShowSettingsEmptyState));
        OnPropertyChanged(nameof(CanEditReviewSettings));
        OnPropertyChanged(nameof(CanSaveReviewSettings));
        OnPropertyChanged(nameof(CanEditGraphSettings));
        OnPropertyChanged(nameof(CanSaveGraphSettings));
        RaiseSettingsCommandStates();
    }
}
