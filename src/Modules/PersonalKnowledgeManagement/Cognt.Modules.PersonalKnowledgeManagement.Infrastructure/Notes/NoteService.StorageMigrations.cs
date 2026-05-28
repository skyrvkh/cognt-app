using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static void MigrateLegacyFilesDirectory(
        string root,
        ITreeRepository repository,
        ILogger<NoteService>? logger)
    {
        string legacyDirectory = Path.Combine(root, LegacyFilesDirectoryName);
        string currentDirectory = Path.Combine(root, FilesDirectoryName);
        if (!Directory.Exists(legacyDirectory) || Directory.Exists(currentDirectory))
        {
            return;
        }

        logger?.LogInformation(
            "Migrating legacy Files directory. Root={Root} LegacyDirectory={LegacyDirectory} CurrentDirectory={CurrentDirectory}",
            root,
            legacyDirectory,
            currentDirectory);
        Directory.Move(legacyDirectory, currentDirectory);
        int movedDraftCount = MoveMigratedCardContextDrafts(
            root,
            EnumerateMigratedNotePathPairs(root).ToArray(),
            logger);
        repository.RewriteFileRelPathPrefix(
            $"{LegacyFilesDirectoryName}/",
            $"{FilesDirectoryName}/");
        logger?.LogInformation(
            "Legacy Files directory migration completed. Root={Root} MovedDraftCount={MovedDraftCount}",
            root,
            movedDraftCount);
    }

    private static IEnumerable<(string OldRelPath, string NewRelPath)> EnumerateMigratedNotePathPairs(string root)
    {
        string currentDirectory = Path.Combine(root, FilesDirectoryName);
        if (!Directory.Exists(currentDirectory))
        {
            yield break;
        }

        foreach (string filePath in EnumerateSupportedNoteFiles(currentDirectory, SearchOption.AllDirectories))
        {
            string newRelPath = NormalizeRelativePath(Path.GetRelativePath(root, filePath));
            if (IsCardContextDraftPath(newRelPath))
            {
                continue;
            }

            string? oldRelPath = TrySwapFirstPathSegment(
                newRelPath,
                FilesDirectoryName,
                LegacyFilesDirectoryName);
            if (oldRelPath is not null)
            {
                yield return (oldRelPath, newRelPath);
            }
        }
    }

    private static int MoveMigratedCardContextDrafts(
        string root,
        IEnumerable<(string OldRelPath, string NewRelPath)> migratedNotePaths,
        ILogger<NoteService>? logger)
    {
        int movedDraftCount = 0;
        foreach ((string oldRelPath, string newRelPath) in migratedNotePaths)
        {
            string oldDraftPath = GetCardContextDraftMigrationPath(
                root,
                FilesDirectoryName,
                oldRelPath);
            if (!File.Exists(oldDraftPath))
            {
                continue;
            }

            string newDraftPath = GetCardContextDraftMigrationPath(
                root,
                FilesDirectoryName,
                newRelPath);
            if (File.Exists(newDraftPath))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(newDraftPath)
                ?? throw new InvalidOperationException("The card-context sidecar path is invalid."));
            File.Move(oldDraftPath, newDraftPath);
            movedDraftCount++;
        }

        if (movedDraftCount > 0)
        {
            logger?.LogInformation(
                "Migrated card-context draft files. Root={Root} DraftCount={DraftCount}",
                root,
                movedDraftCount);
        }

        return movedDraftCount;
    }

    private static bool IsCardContextDraftPath(string normalizedRelativePath)
    {
        return normalizedRelativePath.Contains($"/{CardContextsDirectoryName}/", StringComparison.OrdinalIgnoreCase);
    }
}
