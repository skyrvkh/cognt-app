using System.IO;
using System.Text.RegularExpressions;
using CogntApp.Models.Markdown;

namespace CogntApp.Services;

public interface INoteService
{
    string CreateUniqueNotePath(string notesDirectory);

    string CreateExtractFile(CreateExtractFileRequest request);

    NoteGroupResult GroupNoteWithTarget(
        string notesDirectory,
        string droppedNotePath,
        string targetNotePath);

    string RenameMarkdownFile(
        string notesDirectory,
        string notePath,
        string newName);

    string RenameFolder(
        string notesDirectory,
        string folderPath,
        string newName);

    void DeleteMarkdownFile(
        string notesDirectory,
        string notePath);

    void DeleteFolder(
        string notesDirectory,
        string folderPath);

    void SaveNote(string notePath, string content);

    IReadOnlyList<MentionTarget> ListMentionTargets(
        string notesDirectory,
        IReadOnlyDictionary<string, int>? recentRanks = null);

    MentionPreview ReadMentionPreview(
        string notesDirectory,
        string relativePath,
        int maxCharacters);

    string ResolveMentionPath(string notesDirectory, string relativePath);
}

public sealed class NoteService : INoteService
{
    private static readonly Regex HeadingRegex = new(@"^\s{0,3}#{1,6}\s+(.+?)\s*#*\s*$", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

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
        string content = EnsureTrailingNewline(request.SelectedContent.Trim());

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

    public NoteGroupResult GroupNoteWithTarget(
        string notesDirectory,
        string droppedNotePath,
        string targetNotePath)
    {
        string root = GetExistingNotesRoot(notesDirectory);
        string droppedPath = ResolveExistingMarkdownPathInRoot(root, droppedNotePath);
        string targetPath = ResolveExistingMarkdownPathInRoot(root, targetNotePath);

        if (string.Equals(droppedPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A note cannot be dropped onto itself.");
        }

        string targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException("The target note does not have a parent directory.");
        string targetName = Path.GetFileNameWithoutExtension(targetPath);
        string folderPath = GetGroupFolderPath(targetDirectory, targetName);

        Directory.CreateDirectory(folderPath);

        string targetDestination = Path.Combine(folderPath, Path.GetFileName(targetPath));
        string droppedDestination = Path.Combine(folderPath, Path.GetFileName(droppedPath));

        EnsureMoveDestinationAvailable(targetPath, targetDestination);
        EnsureMoveDestinationAvailable(droppedPath, droppedDestination);

        string movedTargetPath = MoveMarkdownFile(targetPath, targetDestination);
        string movedDroppedPath = MoveMarkdownFile(droppedPath, droppedDestination);

        return new NoteGroupResult(
            folderPath,
            movedTargetPath,
            movedDroppedPath);
    }

    public string RenameMarkdownFile(
        string notesDirectory,
        string notePath,
        string newName)
    {
        string root = GetExistingNotesRoot(notesDirectory);
        string sourcePath = ResolveExistingMarkdownPathInRoot(root, notePath);
        string fileName = NormalizeMarkdownFileName(newName);
        string destinationPath = Path.Combine(
            Path.GetDirectoryName(sourcePath)
                ?? throw new InvalidOperationException("The Markdown file does not have a parent directory."),
            fileName);

        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath;
        }

        EnsureMoveDestinationAvailable(sourcePath, destinationPath);
        File.Move(sourcePath, destinationPath);
        return Path.GetFullPath(destinationPath);
    }

    public string RenameFolder(
        string notesDirectory,
        string folderPath,
        string newName)
    {
        string root = GetExistingNotesRoot(notesDirectory);
        string sourcePath = ResolveExistingFolderPathInRoot(root, folderPath);
        string folderName = NormalizeFileSystemName(newName, "Folder name");
        string destinationPath = Path.Combine(
            Path.GetDirectoryName(sourcePath)
                ?? throw new InvalidOperationException("The folder does not have a parent directory."),
            folderName);

        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath;
        }

        EnsureFolderDestinationAvailable(sourcePath, destinationPath);
        Directory.Move(sourcePath, destinationPath);
        return Path.GetFullPath(destinationPath);
    }

    public void DeleteMarkdownFile(
        string notesDirectory,
        string notePath)
    {
        string root = GetExistingNotesRoot(notesDirectory);
        string fullPath = ResolveExistingMarkdownPathInRoot(root, notePath);
        File.Delete(fullPath);
    }

    public void DeleteFolder(
        string notesDirectory,
        string folderPath)
    {
        string root = GetExistingNotesRoot(notesDirectory);
        string fullPath = ResolveExistingFolderPathInRoot(root, folderPath);
        Directory.Delete(fullPath, recursive: true);
    }

    private static string GetGroupFolderPath(string targetDirectory, string targetName)
    {
        string currentDirectoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(targetDirectory));

        return string.Equals(currentDirectoryName, targetName, StringComparison.OrdinalIgnoreCase)
            ? targetDirectory
            : Path.Combine(targetDirectory, targetName);
    }

    private static void EnsureMoveDestinationAvailable(string sourcePath, string destinationPath)
    {
        string fullSourcePath = Path.GetFullPath(sourcePath);
        string fullDestinationPath = Path.GetFullPath(destinationPath);

        if (!string.Equals(fullSourcePath, fullDestinationPath, StringComparison.OrdinalIgnoreCase)
            && File.Exists(fullDestinationPath))
        {
            throw new IOException($"A Markdown file already exists at: {fullDestinationPath}");
        }
    }

    private static string MoveMarkdownFile(string sourcePath, string destinationPath)
    {
        string fullSourcePath = Path.GetFullPath(sourcePath);
        string fullDestinationPath = Path.GetFullPath(destinationPath);

        if (string.Equals(fullSourcePath, fullDestinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return fullSourcePath;
        }

        File.Move(fullSourcePath, fullDestinationPath);
        return fullDestinationPath;
    }

    public void SaveNote(string notePath, string content)
    {
        if (string.IsNullOrWhiteSpace(notePath))
        {
            return;
        }

        File.WriteAllText(notePath, content);
    }

    public IReadOnlyList<MentionTarget> ListMentionTargets(
        string notesDirectory,
        IReadOnlyDictionary<string, int>? recentRanks = null)
    {
        if (string.IsNullOrWhiteSpace(notesDirectory) || !Directory.Exists(notesDirectory))
        {
            return [];
        }

        string root = NormalizeRoot(notesDirectory);

        return Directory
            .EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => CreateMentionTarget(root, path, recentRanks))
            .ToArray();
    }

    public MentionPreview ReadMentionPreview(
        string notesDirectory,
        string relativePath,
        int maxCharacters)
    {
        string root = GetExistingNotesRoot(notesDirectory);
        string path = ResolveMentionPath(root, relativePath);
        string content = File.ReadAllText(path);

        return new MentionPreview(
            NormalizeRelativePath(Path.GetRelativePath(root, path)),
            ExtractTitle(content, Path.GetFileName(path)),
            CreateExcerpt(content, Math.Max(0, maxCharacters)));
    }

    public string ResolveMentionPath(string notesDirectory, string relativePath)
    {
        string root = GetExistingNotesRoot(notesDirectory);

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Mention path is required.", nameof(relativePath));
        }

        string platformRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(platformRelativePath))
        {
            throw new UnauthorizedAccessException("Mention paths must be relative to the open Markdown root.");
        }

        string fullPath = Path.GetFullPath(Path.Combine(root, platformRelativePath));
        if (!IsPathInsideRoot(fullPath, root))
        {
            throw new UnauthorizedAccessException("Mention path is outside the open Markdown root.");
        }

        EnsureMarkdownPath(fullPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The mentioned Markdown file does not exist.", fullPath);
        }

        return fullPath;
    }

    private static string ResolveExistingMarkdownPathInRoot(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Markdown path is required.", nameof(path));
        }

        string fullPath = Path.GetFullPath(path);
        if (!IsPathInsideRoot(fullPath, root))
        {
            throw new UnauthorizedAccessException("Markdown files must be inside the open Markdown root.");
        }

        EnsureMarkdownPath(fullPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The Markdown file does not exist.", fullPath);
        }

        return fullPath;
    }

    private static string ResolveExistingFolderPathInRoot(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Folder path is required.", nameof(path));
        }

        string fullPath = Path.GetFullPath(path);
        if (string.Equals(
                Path.TrimEndingDirectorySeparator(fullPath),
                Path.TrimEndingDirectorySeparator(root),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The open notes root cannot be renamed or deleted.");
        }

        if (!IsPathInsideRoot(fullPath, root))
        {
            throw new UnauthorizedAccessException("Folders must be inside the open Markdown root.");
        }

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"The folder does not exist: {fullPath}");
        }

        return fullPath;
    }

    private static void ValidateExtractRequest(CreateExtractFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedContent))
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
        string fullRoot = NormalizeRoot(root);

        return string.Equals(fullPath, fullCurrentPath, StringComparison.OrdinalIgnoreCase)
            && IsPathInsideRoot(fullPath, fullRoot)
            && string.Equals(Path.GetExtension(fullPath), ".md", StringComparison.OrdinalIgnoreCase);
    }

    private static MentionTarget CreateMentionTarget(
        string root,
        string path,
        IReadOnlyDictionary<string, int>? recentRanks)
    {
        string fullPath = Path.GetFullPath(path);
        string fileName = Path.GetFileName(fullPath);
        string relativePath = NormalizeRelativePath(Path.GetRelativePath(root, fullPath));
        int? recentRank = TryGetRecentRank(fullPath, recentRanks);

        return new MentionTarget(
            relativePath,
            fileName,
            recentRank);
    }

    private static int? TryGetRecentRank(string fullPath, IReadOnlyDictionary<string, int>? recentRanks)
    {
        if (recentRanks is null)
        {
            return null;
        }

        return recentRanks.TryGetValue(fullPath, out int rank)
            ? rank
            : null;
    }

    private static string GetExistingNotesRoot(string notesDirectory)
    {
        if (string.IsNullOrWhiteSpace(notesDirectory))
        {
            throw new InvalidOperationException("No Markdown root is open.");
        }

        string root = NormalizeRoot(notesDirectory);
        return Directory.Exists(root)
            ? root
            : throw new DirectoryNotFoundException($"The notes directory does not exist: {root}");
    }

    private static string NormalizeRoot(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static void EnsureMarkdownPath(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Mentions can only open Markdown files.");
        }
    }

    private static string NormalizeMarkdownFileName(string name)
    {
        string fileName = NormalizeFileSystemName(name, "Markdown file name");

        if (!string.Equals(Path.GetExtension(fileName), ".md", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".md";
        }

        EnsureMarkdownPath(fileName);
        return fileName;
    }

    private static string NormalizeFileSystemName(string name, string label)
    {
        string fileName = name.Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException($"{label} is required.", nameof(name));
        }

        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"{label} contains invalid characters.", nameof(name));
        }

        if (fileName.EndsWith(".", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{label} cannot end with a period.", nameof(name));
        }

        return fileName;
    }

    private static void EnsureFolderDestinationAvailable(string sourcePath, string destinationPath)
    {
        string fullSourcePath = Path.GetFullPath(sourcePath);
        string fullDestinationPath = Path.GetFullPath(destinationPath);

        if (string.Equals(fullSourcePath, fullDestinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Directory.Exists(fullDestinationPath) || File.Exists(fullDestinationPath))
        {
            throw new IOException($"A file or folder already exists at: {fullDestinationPath}");
        }
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

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }

    private static string ExtractTitle(string markdown, string fileName)
    {
        foreach (string line in markdown.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            Match match = HeadingRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            string title = WhitespaceRegex.Replace(match.Groups[1].Value, " ").Trim();
            if (title.Length > 0)
            {
                return title;
            }
        }

        return Path.GetFileNameWithoutExtension(fileName);
    }

    private static string CreateExcerpt(string markdown, int maxCharacters)
    {
        string normalized = markdown.Trim();
        if (maxCharacters == 0 || normalized.Length <= maxCharacters)
        {
            return normalized;
        }

        string candidate = normalized[..maxCharacters];
        int lastLineBreak = candidate.LastIndexOf('\n');
        if (lastLineBreak >= 200)
        {
            candidate = candidate[..lastLineBreak];
        }

        return $"{candidate.TrimEnd()}...";
    }
}
