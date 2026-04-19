using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text;
using CogntApp.Models.Markdown;
using CogntApp.Models.Tree;

namespace CogntApp.Services;

public interface INoteService : IDisposable
{
    void CloseCurrentCollection();

    string ReadNoteFile(string notePath);

    IReadOnlyList<TreeItemRow> LoadRootItems(string notesDirectory);

    IReadOnlyList<TreeItemRow> LoadChildren(string notesDirectory, long parentId);

    TreeItemRow? GetItem(string notesDirectory, long itemId);

    TreeItemRow? FindNoteByPath(string notesDirectory, string notePath);

    long CreateFolder(
        string notesDirectory,
        long? parentId,
        string title,
        long? previousSiblingId = null,
        long? nextSiblingId = null);

    CreatedNote CreateNote(
        string notesDirectory,
        long? parentId,
        string title,
        string initialContent,
        long? previousSiblingId = null,
        long? nextSiblingId = null);

    CreatedNote CreateExtractNote(
        string notesDirectory,
        long? parentId,
        string sourceNotePath,
        string title,
        string initialContent,
        long? previousSiblingId = null,
        long? nextSiblingId = null);

    void MoveItem(
        string notesDirectory,
        long itemId,
        long? newParentId,
        long? previousSiblingId,
        long? nextSiblingId);

    void RenameItem(
        string notesDirectory,
        long itemId,
        string title);

    IReadOnlyList<string> DeleteItem(
        string notesDirectory,
        long itemId);

    IReadOnlyList<string> ImportNoteFiles(
        string notesDirectory,
        IEnumerable<string> sourcePaths);

    void SaveNote(string notePath, string content);

    IReadOnlyList<MentionTarget> ListMentionTargets(
        string notesDirectory,
        IReadOnlyDictionary<string, int>? recentRanks = null);

    IReadOnlyList<MentionTarget> SearchMentionTargets(
        string notesDirectory,
        string query,
        int limit);

    MentionPreview ReadMentionPreview(
        string notesDirectory,
        string relativePath,
        int maxCharacters);

    string ResolveMentionPath(string notesDirectory, string relativePath);
}

public sealed class NoteService : INoteService
{
    private const string MetadataDatabaseFileName = "cognt.sqlite";
    private const string FilesDirectoryName = "Files";
    private const string BlockSuiteNoteExtension = ".json";
    private const string MarkdownNoteExtension = ".md";
    private const string MarkdownImportExtension = ".markdown";

    private static readonly Regex HeadingRegex = new(@"^\s{0,3}#{1,6}\s+(.+?)\s*#*\s*$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions BlockSuiteJsonOptions = new()
    {
        WriteIndented = true
    };
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly object repositoryLock = new();
    private ITreeRepository? treeRepository;
    private string? treeRepositoryRoot;
    private bool disposed;

    public IReadOnlyList<TreeItemRow> LoadRootItems(string notesDirectory)
    {
        ITreeRepository repository = OpenTreeRepository(notesDirectory);
        return repository.LoadRootItems();
    }

    public string ReadNoteFile(string notePath)
    {
        string fullPath = Path.GetFullPath(notePath);
        return File.ReadAllText(fullPath, Encoding.UTF8);
    }

    public void CloseCurrentCollection()
    {
        lock (repositoryLock)
        {
            ThrowIfDisposed();
            CloseTreeRepository();
        }
    }

    public IReadOnlyList<TreeItemRow> LoadChildren(string notesDirectory, long parentId)
    {
        ITreeRepository repository = OpenTreeRepository(notesDirectory);
        return repository.LoadChildren(parentId);
    }

    public TreeItemRow? GetItem(string notesDirectory, long itemId)
    {
        ITreeRepository repository = OpenTreeRepository(notesDirectory);
        return repository.GetItem(itemId);
    }

    public TreeItemRow? FindNoteByPath(string notesDirectory, string notePath)
    {
        string root = GetExistingNotesRoot(notesDirectory);
        string fullPath = ResolveExistingNotePathInRoot(root, notePath);
        string fileRelPath = NormalizeRelativePath(Path.GetRelativePath(root, fullPath));
        ITreeRepository repository = OpenTreeRepository(root);
        return repository.FindNoteByFileRelPath(fileRelPath);
    }

    public long CreateFolder(
        string notesDirectory,
        long? parentId,
        string title,
        long? previousSiblingId = null,
        long? nextSiblingId = null)
    {
        ITreeRepository repository = OpenTreeRepository(notesDirectory);
        return repository.CreateFolder(parentId, title, previousSiblingId, nextSiblingId);
    }

    public CreatedNote CreateNote(
        string notesDirectory,
        long? parentId,
        string title,
        string initialContent,
        long? previousSiblingId = null,
        long? nextSiblingId = null)
    {
        string root = GetExistingNotesRoot(notesDirectory);
        ITreeRepository repository = OpenTreeRepository(root);
        string fileRelPath = CreateUniqueMetadataNoteRelPath(root);

        return CreateNoteFile(
            root,
            repository,
            parentId,
            title,
            initialContent,
            fileRelPath,
            previousSiblingId,
            nextSiblingId);
    }

    public CreatedNote CreateExtractNote(
        string notesDirectory,
        long? parentId,
        string sourceNotePath,
        string title,
        string initialContent,
        long? previousSiblingId = null,
        long? nextSiblingId = null)
    {
        string root = GetExistingNotesRoot(notesDirectory);
        string fullSourcePath = ResolveExistingNotePathInRoot(root, sourceNotePath);
        ITreeRepository repository = OpenTreeRepository(root);
        string fileRelPath = CreateUniqueExtractNoteRelPath(root, fullSourcePath);
        string extractTitle = CreateExtractTitleFromRelPath(fileRelPath);
        string content = IsBlockSuiteDocumentContent(initialContent)
            ? ApplyBlockSuiteDocumentTitle(initialContent, extractTitle)
            : BlockSuiteDocumentContent.FromPlainText(initialContent, extractTitle);

        return CreateNoteFile(
            root,
            repository,
            parentId,
            extractTitle,
            content,
            fileRelPath,
            previousSiblingId,
            nextSiblingId);
    }

    private static CreatedNote CreateNoteFile(
        string root,
        ITreeRepository repository,
        long? parentId,
        string title,
        string initialContent,
        string fileRelPath,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        string notePath = Path.Combine(root, ToPlatformPath(fileRelPath));
        Directory.CreateDirectory(Path.GetDirectoryName(notePath)
            ?? throw new InvalidOperationException("The note file does not have a parent directory."));

        TryWriteNewFile(notePath, EnsureTrailingNewline(initialContent));

        try
        {
            long itemId = repository.CreateNote(parentId, title, fileRelPath, previousSiblingId, nextSiblingId);
            return new CreatedNote(itemId, notePath, fileRelPath);
        }
        catch
        {
            TryDeleteFile(notePath);
            throw;
        }
    }

    public void MoveItem(
        string notesDirectory,
        long itemId,
        long? newParentId,
        long? previousSiblingId,
        long? nextSiblingId)
    {
        ITreeRepository repository = OpenTreeRepository(notesDirectory);
        TreeItemRow item = repository.GetItem(itemId)
            ?? throw new InvalidOperationException($"Tree item {itemId} does not exist.");

        if (item.ParentId == newParentId)
        {
            repository.ReorderItem(itemId, previousSiblingId, nextSiblingId);
            return;
        }

        repository.MoveItem(itemId, newParentId, previousSiblingId, nextSiblingId);
    }

    public void RenameItem(string notesDirectory, long itemId, string title)
    {
        ITreeRepository repository = OpenTreeRepository(notesDirectory);
        repository.RenameItem(itemId, title);
    }

    public IReadOnlyList<string> DeleteItem(string notesDirectory, long itemId)
    {
        string root = GetExistingNotesRoot(notesDirectory);
        ITreeRepository repository = OpenTreeRepository(root);
        IReadOnlyList<string> deletedNotePaths = CollectDeletedNotePaths(root, repository, itemId);

        repository.DeleteItem(itemId);

        foreach (string notePath in deletedNotePaths)
        {
            TryDeleteFile(notePath);
        }

        return deletedNotePaths;
    }

    private static IReadOnlyList<string> CollectDeletedNotePaths(
        string root,
        ITreeRepository repository,
        long itemId)
    {
        TreeItemRow item = repository.GetItem(itemId)
            ?? throw new InvalidOperationException($"Tree item {itemId} does not exist.");
        List<string> notePaths = [];
        HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);

        CollectDeletedNotePaths(root, repository, item, notePaths, seenPaths);
        return notePaths;
    }

    private static void CollectDeletedNotePaths(
        string root,
        ITreeRepository repository,
        TreeItemRow item,
        ICollection<string> notePaths,
        ISet<string> seenPaths)
    {
        if (item.Kind is TreeItemKind.Note or TreeItemKind.PdfDocument && !string.IsNullOrWhiteSpace(item.FileRelPath))
        {
            string notePath = ResolveNotePathForDeletion(root, item.FileRelPath);
            if (seenPaths.Add(notePath))
            {
                notePaths.Add(notePath);
            }
        }

        foreach (TreeItemRow child in repository.LoadChildren(item.Id))
        {
            CollectDeletedNotePaths(root, repository, child, notePaths, seenPaths);
        }
    }

    private static string ResolveNotePathForDeletion(string root, string fileRelPath)
    {
        string fullPath = Path.GetFullPath(Path.Combine(root, ToPlatformPath(fileRelPath)));
        if (!IsPathInsideRoot(fullPath, root))
        {
            throw new InvalidOperationException($"The note file path is outside the collection: {fileRelPath}");
        }

        return fullPath;
    }

    private ITreeRepository OpenTreeRepository(string notesDirectory)
    {
        string root = GetExistingNotesRoot(notesDirectory);

        lock (repositoryLock)
        {
            ThrowIfDisposed();

            if (treeRepository is not null
                && string.Equals(treeRepositoryRoot, root, StringComparison.OrdinalIgnoreCase))
            {
                return treeRepository;
            }

            CloseTreeRepository();
            treeRepositoryRoot = root;
            treeRepository = new PkmTreeRepository(Path.Combine(root, MetadataDatabaseFileName));

            if (!treeRepository.HasAnyItems())
            {
                BootstrapTreeMetadata(root, treeRepository);
            }

            return treeRepository;
        }
    }

    public void Dispose()
    {
        lock (repositoryLock)
        {
            if (disposed)
            {
                return;
            }

            CloseTreeRepository();
            disposed = true;
        }
    }

    private void CloseTreeRepository()
    {
        treeRepository?.Dispose();
        treeRepository = null;
        treeRepositoryRoot = null;
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(NoteService));
        }
    }

    private static void BootstrapTreeMetadata(string root, ITreeRepository repository)
    {
        BootstrapTreeFolder(root, root, parentId: null, repository);

        string filesDirectory = Path.Combine(root, FilesDirectoryName);
        if (Directory.Exists(filesDirectory))
        {
            BootstrapLooseNoteFiles(root, filesDirectory, repository);
        }
    }

    private static void BootstrapTreeFolder(
        string root,
        string folderPath,
        long? parentId,
        ITreeRepository repository)
    {
        IEnumerable<string> childPaths = Directory
            .EnumerateDirectories(folderPath)
            .Where(path => !IsInternalDirectory(path))
            .Concat(EnumerateNoteFiles(folderPath, SearchOption.TopDirectoryOnly));

        foreach (string childPath in childPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (Directory.Exists(childPath))
            {
                long folderId = repository.CreateFolder(parentId, Path.GetFileName(childPath));
                BootstrapTreeFolder(root, childPath, folderId, repository);
                continue;
            }

            string fileRelPath = NormalizeRelativePath(Path.GetRelativePath(root, childPath));
            if (repository.FindNoteByFileRelPath(fileRelPath) is not null)
            {
                continue;
            }

            string title = Path.GetFileNameWithoutExtension(childPath);
            repository.CreateNote(parentId, title, fileRelPath);
        }
    }

    private static void BootstrapLooseNoteFiles(string root, string folderPath, ITreeRepository repository)
    {
        foreach (string filePath in EnumerateNoteFiles(folderPath, SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string fileRelPath = NormalizeRelativePath(Path.GetRelativePath(root, filePath));
            if (repository.FindNoteByFileRelPath(fileRelPath) is not null)
            {
                continue;
            }

            repository.CreateNote(parentId: null, Path.GetFileNameWithoutExtension(filePath), fileRelPath);
        }
    }

    public IReadOnlyList<string> ImportNoteFiles(
        string notesDirectory,
        IEnumerable<string> sourcePaths)
    {
        string root = GetExistingNotesRoot(notesDirectory);
        ITreeRepository repository = OpenTreeRepository(root);
        List<string> importedPaths = [];

        foreach (string sourcePath in sourcePaths)
        {
            string fullSourcePath = Path.GetFullPath(sourcePath);

            if (!File.Exists(fullSourcePath))
            {
                throw new FileNotFoundException("The document to import does not exist.", fullSourcePath);
            }

            if (!IsImportableNoteExtension(fullSourcePath))
            {
                throw new InvalidOperationException("Only Markdown documents or Cognt JSON notes can be imported.");
            }

            string destinationPath = CreateUniqueImportedNotePath(root, fullSourcePath);
            File.Copy(fullSourcePath, destinationPath);
            string fileRelPath = NormalizeRelativePath(Path.GetRelativePath(root, destinationPath));
            string title = Path.GetFileNameWithoutExtension(destinationPath);
            repository.CreateNote(parentId: null, title, fileRelPath);
            importedPaths.Add(Path.GetFullPath(destinationPath));
        }

        return importedPaths;
    }

    private static bool IsInternalDirectory(string path)
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return string.Equals(name, FilesDirectoryName, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateNoteFiles(string directoryPath, SearchOption searchOption)
    {
        return Directory
            .EnumerateFiles(directoryPath, $"*{BlockSuiteNoteExtension}", searchOption)
            .Concat(Directory.EnumerateFiles(directoryPath, $"*{MarkdownNoteExtension}", searchOption))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public void SaveNote(string notePath, string content)
    {
        if (string.IsNullOrWhiteSpace(notePath))
        {
            return;
        }

        File.WriteAllText(notePath, content, Encoding.UTF8);
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
        ITreeRepository repository = OpenTreeRepository(root);

        return repository.LoadNoteItems()
            .Where(item => !string.IsNullOrWhiteSpace(item.FileRelPath))
            .Select(item => CreateMentionTarget(root, item, recentRanks))
            .OfType<MentionTarget>()
            .ToArray();
    }

    public IReadOnlyList<MentionTarget> SearchMentionTargets(
        string notesDirectory,
        string query,
        int limit)
    {
        if (string.IsNullOrWhiteSpace(notesDirectory) || !Directory.Exists(notesDirectory))
        {
            return [];
        }

        string root = NormalizeRoot(notesDirectory);
        ITreeRepository repository = OpenTreeRepository(root);

        return repository.SearchNoteItems(query, limit)
            .Where(item => !string.IsNullOrWhiteSpace(item.FileRelPath))
            .Select(item => CreateMentionTarget(root, item, recentRanks: null))
            .OfType<MentionTarget>()
            .ToArray();
    }

    public MentionPreview ReadMentionPreview(
        string notesDirectory,
        string relativePath,
        int maxCharacters)
    {
        string root = GetExistingNotesRoot(notesDirectory);
        string path = ResolveMentionPath(root, relativePath);
        string normalizedRelativePath = NormalizeRelativePath(Path.GetRelativePath(root, path));
        string content = File.ReadAllText(path, Encoding.UTF8);
        bool isBlockSuiteNote = IsBlockSuiteNotePath(path);
        string title = OpenTreeRepository(root).FindNoteByFileRelPath(normalizedRelativePath)?.Title
            ?? (isBlockSuiteNote
                ? Path.GetFileNameWithoutExtension(path)
                : ExtractTitle(content, Path.GetFileName(path)));

        return new MentionPreview(
            normalizedRelativePath,
            title,
            isBlockSuiteNote
                ? content
                : BlockSuiteDocumentContent.FromPlainText(
                    CreateExcerpt(content, Math.Max(0, maxCharacters)),
                    title));
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
            throw new UnauthorizedAccessException("Mention paths must be relative to the open collection root.");
        }

        string fullPath = Path.GetFullPath(Path.Combine(root, platformRelativePath));
        if (!IsPathInsideRoot(fullPath, root))
        {
            throw new UnauthorizedAccessException("Mention path is outside the open collection root.");
        }

        EnsureNotePath(fullPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The mentioned note file does not exist.", fullPath);
        }

        return fullPath;
    }

    private static string ResolveExistingNotePathInRoot(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Note path is required.", nameof(path));
        }

        string fullPath = Path.GetFullPath(path);
        if (!IsPathInsideRoot(fullPath, root))
        {
            throw new UnauthorizedAccessException("Note files must be inside the open collection root.");
        }

        EnsureNotePath(fullPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The note file does not exist.", fullPath);
        }

        return fullPath;
    }

    private static bool IsSafeCurrentNotePath(string path, string currentPath, string root)
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
            && IsNoteFilePath(fullPath);
    }

    private static MentionTarget? CreateMentionTarget(
        string root,
        TreeItemRow item,
        IReadOnlyDictionary<string, int>? recentRanks)
    {
        if (string.IsNullOrWhiteSpace(item.FileRelPath))
        {
            return null;
        }

        string relativePath = NormalizeRelativePath(item.FileRelPath);
        string fullPath = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsPathInsideRoot(fullPath, root) || !File.Exists(fullPath))
        {
            return null;
        }

        string fileName = Path.GetFileName(fullPath);
        int? recentRank = TryGetRecentRank(fullPath, recentRanks);
        string title = string.IsNullOrWhiteSpace(item.Title)
            ? Path.GetFileNameWithoutExtension(fullPath)
            : item.Title;

        return new MentionTarget(
            relativePath,
            title,
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
            throw new InvalidOperationException("No collection is open.");
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

    private static void EnsureNotePath(string path)
    {
        if (!IsNoteFilePath(path))
        {
            throw new InvalidOperationException("Mentions can only open note files.");
        }
    }

    private static bool IsNoteFilePath(string path)
    {
        string extension = Path.GetExtension(path);
        return string.Equals(extension, BlockSuiteNoteExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, MarkdownNoteExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlockSuiteNotePath(string path)
    {
        return string.Equals(Path.GetExtension(path), BlockSuiteNoteExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateUniqueMetadataNoteRelPath(string root)
    {
        string filesDirectory = Path.Combine(root, FilesDirectoryName);
        Directory.CreateDirectory(filesDirectory);

        string fileRelPath;
        string notePath;

        do
        {
            fileRelPath = NormalizeRelativePath(Path.Combine(
                FilesDirectoryName,
                $"{CreateGuidV7FileStem()}{BlockSuiteNoteExtension}"));
            notePath = Path.Combine(root, ToPlatformPath(fileRelPath));
        }
        while (File.Exists(notePath));

        return fileRelPath;
    }

    private static string CreateUniqueExtractNoteRelPath(string root, string sourceNotePath)
    {
        string filesDirectory = Path.Combine(root, FilesDirectoryName);
        Directory.CreateDirectory(filesDirectory);

        string sourceStem = CreateSourceFileStem(sourceNotePath);
        string fileRelPath;
        string notePath;

        do
        {
            fileRelPath = NormalizeRelativePath(Path.Combine(
                FilesDirectoryName,
                $"{sourceStem}.{CreateShortUniqueIdentifier()}{BlockSuiteNoteExtension}"));
            notePath = Path.Combine(root, ToPlatformPath(fileRelPath));
        }
        while (File.Exists(notePath));

        return fileRelPath;
    }

    private static string CreateExtractTitleFromRelPath(string fileRelPath)
    {
        string stem = Path.GetFileNameWithoutExtension(fileRelPath).Trim();
        int separatorIndex = stem.LastIndexOf('.');

        return separatorIndex >= 0 && separatorIndex < stem.Length - 1
            ? stem[(separatorIndex + 1)..]
            : stem;
    }

    private static string ApplyBlockSuiteDocumentTitle(string content, string title)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        try
        {
            JsonNode? node = JsonNode.Parse(content);
            if (node is not JsonObject root)
            {
                return content;
            }

            bool changed = false;
            JsonObject? snapshot = root["snapshot"] as JsonObject;
            JsonObject? meta = snapshot?["meta"] as JsonObject;
            if (meta is not null)
            {
                meta["title"] = title;
                changed = true;
            }

            JsonObject? blocks = snapshot?["blocks"] as JsonObject;
            JsonObject? props = blocks?["props"] as JsonObject;
            if (props is not null)
            {
                props["title"] = CreateBlockSuiteTextValue(title);
                changed = true;
            }

            return changed
                ? root.ToJsonString(BlockSuiteJsonOptions) + Environment.NewLine
                : content;
        }
        catch (JsonException)
        {
            return content;
        }
    }

    private static bool IsBlockSuiteDocumentContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            return JsonNode.Parse(content) is JsonObject root
                && string.Equals(root["kind"]?.GetValue<string>(), BlockSuiteDocumentContent.Kind, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonObject CreateBlockSuiteTextValue(string value)
    {
        JsonArray delta = [];
        if (!string.IsNullOrEmpty(value))
        {
            delta.Add(new JsonObject
            {
                ["insert"] = value
            });
        }

        return new JsonObject
        {
            ["$blocksuite:internal:text$"] = true,
            ["delta"] = delta
        };
    }

    private static string CreateSourceFileStem(string sourceNotePath)
    {
        string stem = Path.GetFileNameWithoutExtension(sourceNotePath).Trim();
        if (Guid.TryParse(stem, out Guid guid))
        {
            return guid.ToString("N");
        }

        return NormalizeFileSystemName(stem, "Source note file name");
    }

    private static string CreateGuidV7FileStem()
    {
        return Guid.CreateVersion7().ToString("N");
    }

    private static string CreateShortUniqueIdentifier()
    {
        return Guid.CreateVersion7().ToString("N")[^12..];
    }

    private static string CreateUniqueImportedNotePath(string notesDirectory, string sourcePath)
    {
        string baseName = NormalizeFileSystemName(
            Path.GetFileNameWithoutExtension(sourcePath),
            "Document file name");
        string extension = NormalizeImportedNoteExtension(sourcePath);
        string destinationPath = Path.Combine(notesDirectory, $"{baseName}{extension}");
        int suffix = 2;

        while (File.Exists(destinationPath) || Directory.Exists(destinationPath))
        {
            destinationPath = Path.Combine(notesDirectory, $"{baseName}-{suffix}{extension}");
            suffix++;
        }

        return destinationPath;
    }

    private static bool IsImportableNoteExtension(string path)
    {
        string extension = Path.GetExtension(path);

        return string.Equals(extension, MarkdownNoteExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, MarkdownImportExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, BlockSuiteNoteExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeImportedNoteExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return string.Equals(extension, BlockSuiteNoteExtension, StringComparison.OrdinalIgnoreCase)
            ? BlockSuiteNoteExtension
            : MarkdownNoteExtension;
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
            using StreamWriter writer = new(stream, Encoding.UTF8);
            writer.Write(content);
            return true;
        }
        catch (IOException) when (File.Exists(path))
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
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

    private static string ToPlatformPath(string relativePath)
    {
        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }

    private static string ExtractTitle(string markdown, string fileName)
    {
        return TryExtractHeadingTitle(markdown) ?? Path.GetFileNameWithoutExtension(fileName);
    }

    private static string? TryExtractHeadingTitle(string markdown)
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

        return null;
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
