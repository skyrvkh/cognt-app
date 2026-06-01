using System.IO;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels;

internal static class CollectionCountReader
{
    internal static (int NoteCount, int CardCount) Read(
        string collectionPath,
        Func<string, int> readNoteCount,
        Func<string, int> readCardCount,
        ILogger? logger = null)
    {
        return (
            ReadCount(collectionPath, readNoteCount, "note", logger),
            ReadCount(collectionPath, readCardCount, "card", logger));
    }

    private static int ReadCount(
        string collectionPath,
        Func<string, int> readCount,
        string countKind,
        ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(collectionPath) || !Directory.Exists(collectionPath))
        {
            return 0;
        }

        try
        {
            return readCount(collectionPath);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Collection count read failed; returning zero. CollectionPath={CollectionPath} CountKind={CountKind}",
                collectionPath,
                countKind);
            return 0;
        }
    }
}
