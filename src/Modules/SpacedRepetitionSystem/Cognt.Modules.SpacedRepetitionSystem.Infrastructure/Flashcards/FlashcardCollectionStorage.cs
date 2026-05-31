using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

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

    internal static SqliteFlashcardRepository OpenRepositoryForCollectionId(
        string collectionId,
        ILogger? logger = null)
    {
        return OpenRepositoryForCollectionPath(GetExistingCollectionPath(collectionId), logger);
    }

    internal static SqliteFlashcardRepository OpenRepositoryForCollectionPath(
        string collectionPath,
        ILogger? logger = null)
    {
        string normalizedCollectionPath = Path.GetFullPath(collectionPath);
        MigrateLegacyFlashcardsDirectory(normalizedCollectionPath, logger);

        SqliteFlashcardRepository repository = new(GetDatabasePath(normalizedCollectionPath), logger);
        if (IsExactChildDirectory(normalizedCollectionPath, FlashcardsDirectoryName)
            && !IsExactChildDirectory(normalizedCollectionPath, LegacyFlashcardsDirectoryName))
        {
            repository.RewriteCardFrontRelPathPrefix(
                $"{LegacyFlashcardsDirectoryName}/",
                $"{FlashcardsDirectoryName}/");
        }

        if (IsExactChildDirectory(normalizedCollectionPath, SourceNotesDirectoryName)
            && !IsExactChildDirectory(normalizedCollectionPath, LegacySourceNotesDirectoryName))
        {
            repository.RewriteCardSourceRelPathPrefix(
                $"{LegacySourceNotesDirectoryName}/",
                $"{SourceNotesDirectoryName}/");
        }

        return repository;
    }

    internal static SqliteFlashcardRepository OpenPrunedRepositoryForCollectionId(
        string collectionId,
        ILogger? logger = null)
    {
        return OpenPrunedRepositoryForCollectionPath(GetExistingCollectionPath(collectionId), logger);
    }

    internal static SqliteFlashcardRepository OpenPrunedRepositoryForCollectionPath(
        string collectionPath,
        ILogger? logger = null)
    {
        SqliteFlashcardRepository repository = OpenRepositoryForCollectionPath(collectionPath, logger);
        PruneMissingActiveCardFiles(collectionPath, repository, logger);
        return repository;
    }

    internal static string CreateFlashcardRelPath()
    {
        string cardUid = Guid.CreateVersion7().ToString("N");
        return $"{FlashcardsDirectoryName}/{cardUid}.json";
    }

    internal static void WriteFlashcardFile(
        string root,
        string relPath,
        string content)
    {
        string fullPath = GetFlashcardFullPath(root, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    internal static string CopyFlashcardFile(
        string sourceRoot,
        string sourceRelPath,
        string destinationRoot)
    {
        string sourceFullPath = GetExistingFlashcardFullPath(sourceRoot, sourceRelPath);
        string destinationRelPath = CreateFlashcardRelPath();
        string destinationFullPath = GetFlashcardFullPath(destinationRoot, destinationRelPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFullPath)!);
        File.Copy(sourceFullPath, destinationFullPath, overwrite: false);
        return destinationRelPath;
    }

    internal static string ReadFlashcardFile(string root, string relPath)
    {
        string fullPath = GetExistingFlashcardFullPath(root, relPath);
        return File.ReadAllText(fullPath);
    }

    internal static bool TryLoadMaterializedCard(
        string root,
        long cardId,
        string frontRelPath,
        ICollection<long> staleCardIds,
        out MaterializedFlashcardContent materialized)
    {
        try
        {
            materialized = LoadMaterializedContent(root, frontRelPath);
            return true;
        }
        catch (IOException ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            materialized = null!;
        }

        staleCardIds.Add(cardId);
        return false;
    }
}
