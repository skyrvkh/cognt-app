using System.IO;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    internal Navigation.CollectionRemovalPlan GetCurrentCollectionRemovalPlan()
    {
        string collectionPath = CurrentCollectionPath;
        if (string.IsNullOrWhiteSpace(openCollectionPath)
            || string.IsNullOrWhiteSpace(collectionPath)
            || !Directory.Exists(collectionPath))
        {
            return Navigation.CollectionRemovalPlan.Blocked("No collection is open.");
        }

        (int noteCount, int cardCount) = GetCollectionCounts(collectionPath);
        string collectionName = FormatCollectionName(collectionPath);

        if (noteCount > 0 || cardCount > 0)
        {
            return Navigation.CollectionRemovalPlan.Blocked(
                $"The collection \"{collectionName}\" contains {CountDisplayFormatter.Format(noteCount, "note")} and {CountDisplayFormatter.Format(cardCount, "card")}. Move or delete that content before removing the collection.");
        }

        return Navigation.CollectionRemovalPlan.Allowed(
            $"Remove empty collection \"{collectionName}\"?{Environment.NewLine}{Environment.NewLine}This deletes the collection folder and app metadata. No notes or cards were found in this collection.");
    }

    private void RefreshAvailableCollections()
    {
        string selectedPath = CurrentCollectionPath;
        SortedSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

        AddCollectionPath(paths, selectedPath);
        AddCollectionPath(paths, CollectionPath);

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
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

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

            AvailableCollections.Add(new Navigation.CollectionOptionViewModel(
                path,
                FormatCollectionName(path),
                path,
                isSelected,
                noteCount,
                cardCount));
        }

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
    }
}
