using System.IO;

namespace CogntApp.Shell.DocumentImport;

internal sealed partial class DocumentImportViewModel
{
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

        Files.Add(new DocumentImportQueueItem(
            fullPath,
            file.Length,
            DocumentImportPolicy.GetImportType(fullPath)));
        return string.Empty;
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
