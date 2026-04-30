using System.IO;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.ReadModels;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void EnsureDefaultCollectionExists(string defaultCollectionPath)
    {
        if (Directory.Exists(defaultCollectionPath) || !collectionPathResolver.IsCollectionPath(defaultCollectionPath))
        {
            return;
        }

        string? parentPath = GetCollectionParentPath(defaultCollectionPath);
        if (!string.IsNullOrWhiteSpace(parentPath) && Directory.Exists(parentPath))
        {
            try
            {
                bool hasExistingCollection = Directory
                    .EnumerateDirectories(
                        parentPath,
                        $"*{collectionPathResolver.CollectionDirectoryExtension}",
                        SearchOption.TopDirectoryOnly)
                    .Any();

                if (hasExistingCollection)
                {
                    return;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        Directory.CreateDirectory(defaultCollectionPath);
    }

    private (int NoteCount, int CardCount) GetCollectionCounts(string collectionPath)
    {
        return (
            CountCollectionNotes(collectionPath),
            CountCollectionCards(collectionPath));
    }

    private int CountCollectionNotes(string collectionPath)
    {
        if (string.IsNullOrWhiteSpace(collectionPath) || !Directory.Exists(collectionPath))
        {
            return 0;
        }

        try
        {
            return LoadNoteCatalog(collectionPath).Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Collection note count failed for {collectionPath}: {ex}");
            return 0;
        }
    }

    private int CountCollectionCards(string collectionPath)
    {
        if (string.IsNullOrWhiteSpace(collectionPath) || !Directory.Exists(collectionPath))
        {
            return 0;
        }

        try
        {
            return collectionProgressAnalyticsDashboardQuery.Execute(
                new CollectionProgressAnalyticsDashboardQueryRequest(
                    collectionPath,
                    DateOnly.FromDateTime(DateTime.Today)))
                .TotalCards;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Collection card count failed for {collectionPath}: {ex}");
            return 0;
        }
    }

}
