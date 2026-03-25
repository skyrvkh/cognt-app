using System.IO;

namespace CogntApp.Services;

public interface IMarkdownNoteService
{
    string CreateUniqueNotePath(string notesDirectory);

    string CreateExtractFile(CreateExtractFileRequest request);

    void SaveNote(string notePath, string content);
}

public sealed record CreateExtractFileRequest(
    string RootDirectory,
    string CurrentNotePath,
    string SourcePath,
    string SelectedMarkdown);

public sealed class MarkdownNoteService : IMarkdownNoteService
{
    public string CreateUniqueNotePath(string notesDirectory)
    {
        if (string.IsNullOrWhiteSpace(notesDirectory))
        {
            throw new ArgumentException("Notes directory is required.", nameof(notesDirectory));
        }

        string notePath = Path.Combine(notesDirectory, "untitled.md");
        int suffix = 2;

        while (File.Exists(notePath))
        {
            notePath = Path.Combine(notesDirectory, $"untitled-{suffix}.md");
            suffix++;
        }

        return notePath;
    }

    public string CreateExtractFile(CreateExtractFileRequest request)
    {
        ValidateExtractRequest(request);

        string fullSourcePath = Path.GetFullPath(request.SourcePath);
        string directory = Path.GetDirectoryName(fullSourcePath)
            ?? throw new InvalidOperationException("The source file does not have a parent directory.");
        string sourceName = Path.GetFileNameWithoutExtension(fullSourcePath);
        string content = EnsureTrailingNewline(request.SelectedMarkdown.Trim());

        for (int index = 1; index < 10_000; index++)
        {
            string candidate = Path.Combine(directory, $"{sourceName}-extract-{index:000}.md");
            if (TryWriteNewFile(candidate, content))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not create an extract file because all generated names are taken.");
    }

    public void SaveNote(string notePath, string content)
    {
        if (string.IsNullOrWhiteSpace(notePath))
        {
            return;
        }

        File.WriteAllText(notePath, content);
    }

    private static void ValidateExtractRequest(CreateExtractFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedMarkdown))
        {
            throw new InvalidOperationException("No selected Markdown content.");
        }

        if (!IsSafeCurrentMarkdownPath(request.SourcePath, request.CurrentNotePath, request.RootDirectory))
        {
            throw new InvalidOperationException("The extract source is not the current Markdown file.");
        }
    }

    private static bool IsSafeCurrentMarkdownPath(string path, string currentPath, string root)
    {
        if (string.IsNullOrWhiteSpace(path)
            || string.IsNullOrWhiteSpace(currentPath)
            || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(path);
        string fullCurrentPath = Path.GetFullPath(currentPath);
        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        return string.Equals(fullPath, fullCurrentPath, StringComparison.OrdinalIgnoreCase)
            && IsPathInsideRoot(fullPath, fullRoot)
            && string.Equals(Path.GetExtension(fullPath), ".md", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPathInsideRoot(string fullPath, string fullRoot)
    {
        string rootWithSeparator = $"{fullRoot}{Path.DirectorySeparatorChar}";

        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryWriteNewFile(string path, string content)
    {
        try
        {
            using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write);
            using StreamWriter writer = new(stream);
            writer.Write(content);
            return true;
        }
        catch (IOException) when (File.Exists(path))
        {
            return false;
        }
    }

    private static string EnsureTrailingNewline(string content)
    {
        return content.Length == 0 || content.EndsWith(Environment.NewLine, StringComparison.Ordinal)
            ? content
            : $"{content}{Environment.NewLine}";
    }
}
