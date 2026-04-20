using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static void BootstrapTreeMetadata(string root, ITreeRepository repository)
    {
        BootstrapFolderTreeItems(root, root, parentId: null, repository);

        foreach (string noteFilesDirectoryName in GetKnownLooseNoteDirectoryNames())
        {
            string filesDirectory = Path.Combine(root, noteFilesDirectoryName);
            if (Directory.Exists(filesDirectory))
            {
                BootstrapLooseCollectionNotes(root, filesDirectory, repository);
            }
        }
    }

    private static void BootstrapFolderTreeItems(
        string root,
        string folderPath,
        long? parentId,
        ITreeRepository repository)
    {
        IEnumerable<string> childPaths = Directory
            .EnumerateDirectories(folderPath)
            .Where(path => !IsRepositoryMetadataDirectory(path))
            .Concat(EnumerateSupportedNoteFiles(folderPath, SearchOption.TopDirectoryOnly));

        foreach (string childPath in childPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (Directory.Exists(childPath))
            {
                long folderId = repository.CreateFolder(parentId, Path.GetFileName(childPath));
                BootstrapFolderTreeItems(root, childPath, folderId, repository);
                continue;
            }

            TryCreateBootstrappedNote(root, childPath, parentId, repository);
        }
    }

    private static void BootstrapLooseCollectionNotes(string root, string folderPath, ITreeRepository repository)
    {
        foreach (string filePath in EnumerateSupportedNoteFiles(folderPath, SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            TryCreateBootstrappedNote(root, filePath, parentId: null, repository);
        }
    }

    private static void TryCreateBootstrappedNote(
        string root,
        string filePath,
        long? parentId,
        ITreeRepository repository)
    {
        string fileRelPath = NormalizeRelativePath(Path.GetRelativePath(root, filePath));
        if (repository.FindNoteByFileRelPath(fileRelPath) is not null)
        {
            return;
        }

        repository.CreateNote(parentId, Path.GetFileNameWithoutExtension(filePath), fileRelPath);
    }
}
