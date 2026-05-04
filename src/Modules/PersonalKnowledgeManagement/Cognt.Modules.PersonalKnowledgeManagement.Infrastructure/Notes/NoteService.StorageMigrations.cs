using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static void MigrateLegacyFilesDirectory(string root, ITreeRepository repository)
    {
        string legacyDirectory = Path.Combine(root, LegacyFilesDirectoryName);
        string currentDirectory = Path.Combine(root, FilesDirectoryName);
        if (!Directory.Exists(legacyDirectory) || Directory.Exists(currentDirectory))
        {
            return;
        }

        Directory.Move(legacyDirectory, currentDirectory);
        MoveMigratedCardContextDrafts(root, EnumerateMigratedNotePathPairs(root).ToArray());
        repository.RewriteFileRelPathPrefix(
            $"{LegacyFilesDirectoryName}/",
            $"{FilesDirectoryName}/");
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

    private static void MoveMigratedCardContextDrafts(
        string root,
        IEnumerable<(string OldRelPath, string NewRelPath)> migratedNotePaths)
    {
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
        }
    }

    private static bool IsCardContextDraftPath(string normalizedRelativePath)
    {
        return normalizedRelativePath.Contains($"/{CardContextsDirectoryName}/", StringComparison.OrdinalIgnoreCase);
    }
}
