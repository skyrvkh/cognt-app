using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal static partial class FlashcardCollectionStorage
{
    private const string MetadataDatabaseFileName = "cognt.sqlite";
    private const string FlashcardsDirectoryName = "flashcards";
    private const string LegacyFlashcardsDirectoryName = "Flashcards";
    private const string SourceNotesDirectoryName = "collection";
    private const string LegacySourceNotesDirectoryName = "Files";

    internal static string GetExistingCollectionPath(string collectionId)
    {
        string collectionPath = Path.GetFullPath(collectionId);
        if (!Directory.Exists(collectionPath))
        {
            throw new DirectoryNotFoundException($"The collection directory does not exist: {collectionPath}");
        }

        return collectionPath;
    }

    internal static SqliteFlashcardRepository OpenRepositoryForCollectionId(string collectionId)
    {
        return OpenRepositoryForCollectionPath(GetExistingCollectionPath(collectionId));
    }

    internal static SqliteFlashcardRepository OpenRepositoryForCollectionPath(string collectionPath)
    {
        string normalizedCollectionPath = Path.GetFullPath(collectionPath);
        MigrateLegacyFlashcardsDirectory(normalizedCollectionPath);

        return new SqliteFlashcardRepository(GetDatabasePath(normalizedCollectionPath));
    }

    internal static string CreateFlashcardRelPath()
    {
        string cardUid = Guid.CreateVersion7().ToString("N");
        return $"{FlashcardsDirectoryName}/{cardUid}.json";
    }

    internal static void WriteFlashcardFile(string root, string relPath, string content)
    {
        string fullPath = GetFlashcardFullPath(root, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    internal static string ReadFlashcardFile(string root, string relPath)
    {
        string fullPath = GetExistingFlashcardFullPath(root, relPath);
        return File.ReadAllText(fullPath);
    }

}
