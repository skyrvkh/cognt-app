using System.IO;

namespace CogntApp.Shell.DocumentImport;

internal sealed partial class DocumentImportViewModel
{
    private void QueueImportPath(
        string path,
        HashSet<string> existingPaths,
        ICollection<string> rejected)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            rejected.Add("A selected path was empty.");
            return;
        }

        string fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            QueueFolder(fullPath, existingPaths, rejected);
            return;
        }

        string addError = TryQueueFile(fullPath, existingPaths);
        if (!string.IsNullOrWhiteSpace(addError))
        {
            rejected.Add(addError);
        }
    }

    private void QueueFolder(
        string folderPath,
        HashSet<string> existingPaths,
        ICollection<string> rejected)
    {
        string folderName = GetDisplayName(folderPath);
        if (DocumentImportPolicy.IsCollectionFolderPath(folderPath))
        {
            rejected.Add($"{folderName} is a Cognt collection. Drop the collection folder by itself to merge notes and flashcards.");
            return;
        }

        int supportedFileCount = 0;
        foreach (string filePath in EnumerateFolderFiles(folderPath, rejected))
        {
            if (!DocumentImportPolicy.IsSupportedPath(filePath))
            {
                continue;
            }

            supportedFileCount++;
            string addError = TryQueueFile(filePath, existingPaths);
            if (!string.IsNullOrWhiteSpace(addError))
            {
                rejected.Add(addError);
            }
        }

        if (supportedFileCount == 0)
        {
            rejected.Add($"{folderName} did not contain DOCX, PDF, or MD files.");
        }
    }

    private string TryQueueFile(string path, HashSet<string> existingPaths)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "A selected file path was empty.";
        }

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return $"{Path.GetFileName(path)} was not found.";
        }

        if (!DocumentImportPolicy.IsSupportedPath(fullPath))
        {
            return $"{Path.GetFileName(path)} is not supported. Add a DOCX, PDF, or MD file.";
        }

        FileInfo file = new(fullPath);
        if (file.Length == 0)
        {
            return $"{file.Name} is empty.";
        }

        if (!existingPaths.Add(fullPath))
        {
            return $"{file.Name} is already in the import queue.";
        }

        DocumentImportType importType = DocumentImportPolicy.GetImportType(fullPath);
        Files.Add(new DocumentImportQueueItem(
            fullPath,
            file.Length,
            importType));
        return string.Empty;
    }

    private static IEnumerable<string> EnumerateFolderFiles(
        string folderPath,
        ICollection<string> rejected)
    {
        Stack<string> pendingFolders = new();
        pendingFolders.Push(folderPath);

        while (pendingFolders.Count > 0)
        {
            string currentFolder = pendingFolders.Pop();
            string[] files;
            try
            {
                files = Directory.GetFiles(currentFolder);
            }
            catch (Exception ex) when (IsRecoverableDirectoryReadException(ex))
            {
                rejected.Add($"{GetDisplayName(currentFolder)} could not be read: {ex.Message}");
                continue;
            }

            foreach (string file in files)
            {
                yield return file;
            }

            string[] childFolders;
            try
            {
                childFolders = Directory.GetDirectories(currentFolder);
            }
            catch (Exception ex) when (IsRecoverableDirectoryReadException(ex))
            {
                rejected.Add($"{GetDisplayName(currentFolder)} could not be searched: {ex.Message}");
                continue;
            }

            for (int index = childFolders.Length - 1; index >= 0; index--)
            {
                pendingFolders.Push(childFolders[index]);
            }
        }
    }

    private static bool IsRecoverableDirectoryReadException(Exception ex)
    {
        return ex is IOException
            or UnauthorizedAccessException
            or DirectoryNotFoundException
            or PathTooLongException
            or NotSupportedException;
    }

    private static string GetDisplayName(string path)
    {
        string trimmedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmedPath) is { Length: > 0 } name
            ? name
            : trimmedPath;
    }

    private void RemoveFile(object? parameter)
    {
        if (parameter is not DocumentImportQueueItem file || !file.CanRemove)
        {
            return;
        }

        Files.Remove(file);
        UpdateMessageState(string.Empty, isError: false);
        RefreshDerivedState();
    }
}
