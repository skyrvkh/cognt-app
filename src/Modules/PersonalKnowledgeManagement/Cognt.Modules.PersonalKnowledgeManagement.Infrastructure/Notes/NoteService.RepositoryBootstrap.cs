using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static void BootstrapTreeMetadata(string root, ITreeRepository repository, ILogger<NoteService>? logger = null)
    {
        int createdItemCount = BootstrapFolderTreeItems(root, root, parentId: null, repository);

        foreach (string noteFilesDirectoryName in GetKnownLooseNoteDirectoryNames())
        {
            string filesDirectory = Path.Combine(root, noteFilesDirectoryName);
            if (Directory.Exists(filesDirectory))
            {
                createdItemCount += BootstrapLooseCollectionNotes(root, filesDirectory, repository);
            }
        }

        if (createdItemCount > 0)
        {
            logger?.LogInformation(
                "Tree metadata bootstrap completed. Root={Root} CreatedItemCount={CreatedItemCount}",
                root,
                createdItemCount);
        }
    }

    private static int BootstrapFolderTreeItems(
        string root,
        string folderPath,
        long? parentId,
        ITreeRepository repository)
    {
        IEnumerable<string> childPaths = Directory
            .EnumerateDirectories(folderPath)
            .Where(path => !IsRepositoryMetadataDirectory(path))
            .Concat(EnumerateSupportedNoteFiles(folderPath, SearchOption.TopDirectoryOnly));

        int createdItemCount = 0;
        foreach (string childPath in childPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (Directory.Exists(childPath))
            {
                long folderId = repository.CreateFolder(parentId, Path.GetFileName(childPath));
                createdItemCount++;
                createdItemCount += BootstrapFolderTreeItems(root, childPath, folderId, repository);
                continue;
            }

            if (TryCreateBootstrappedNote(root, childPath, parentId, repository))
            {
                createdItemCount++;
            }
        }

        return createdItemCount;
    }

    private static int BootstrapLooseCollectionNotes(
        string root,
        string folderPath,
        ITreeRepository repository)
    {
        int createdItemCount = 0;
        foreach (string filePath in EnumerateSupportedNoteFiles(folderPath, SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (TryCreateBootstrappedNote(root, filePath, parentId: null, repository))
            {
                createdItemCount++;
            }
        }

        return createdItemCount;
    }

    private static bool TryCreateBootstrappedNote(
        string root,
        string filePath,
        long? parentId,
        ITreeRepository repository)
    {
        string fileRelPath = NormalizeRelativePath(Path.GetRelativePath(root, filePath));
        if (repository.FindNoteByFileRelPath(fileRelPath) is not null)
        {
            return false;
        }

        repository.CreateNote(parentId, Path.GetFileNameWithoutExtension(filePath), fileRelPath);
        return true;
    }
}
