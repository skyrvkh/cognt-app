using System.IO;
using System.Text.RegularExpressions;

namespace CogntApp.Services;

public interface IMarkdownNoteService
{
    string CreateUniqueNotePath(string notesDirectory);

    string CreateExtractFile(CreateExtractFileRequest request);

    void SaveNote(string notePath, string content);

    IReadOnlyList<MarkdownMentionTarget> ListMentionTargets(
        string notesDirectory,
        IReadOnlyDictionary<string, int>? recentRanks = null);

    MarkdownMentionPreview ReadMentionPreview(
        string notesDirectory,
        string relativePath,
        int maxCharacters);

    string ResolveMentionPath(string notesDirectory, string relativePath);
}

public sealed record CreateExtractFileRequest(
    string RootDirectory,
    string CurrentNotePath,
    string SourcePath,
    string SelectedMarkdown);

public sealed record MarkdownMentionTarget(
    string Path,
    string RelativePath,
    string FileName,
    string Title,
    int? RecentRank);

public sealed record MarkdownMentionPreview(
    string RelativePath,
    string Title,
    string MarkdownExcerpt);

public sealed class MarkdownNoteService : IMarkdownNoteService
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

    public IReadOnlyList<MarkdownMentionTarget> ListMentionTargets(
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

    public MarkdownMentionPreview ReadMentionPreview(
        string notesDirectory,
        string relativePath,
        int maxCharacters)
    {
        string root = GetExistingNotesRoot(notesDirectory);
        string path = ResolveMentionPath(root, relativePath);
        string content = File.ReadAllText(path);

        return new MarkdownMentionPreview(
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

    private static MarkdownMentionTarget CreateMentionTarget(
        string root,
        string path,
        IReadOnlyDictionary<string, int>? recentRanks)
    {
        string fullPath = Path.GetFullPath(path);
        string fileName = Path.GetFileName(fullPath);
        string relativePath = NormalizeRelativePath(Path.GetRelativePath(root, fullPath));
        int? recentRank = TryGetRecentRank(fullPath, recentRanks);

        return new MarkdownMentionTarget(
            fullPath,
            relativePath,
            fileName,
            ExtractTitle(File.ReadAllText(fullPath), fileName),
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