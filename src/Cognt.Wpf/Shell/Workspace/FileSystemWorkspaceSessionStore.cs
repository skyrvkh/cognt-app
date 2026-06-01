using System.IO;
using System.Text.Json;
using Cognt.Wpf.EditorHost.Models.Editor;
using Microsoft.Extensions.Logging;

namespace CogntApp.Shell.Workspace;

internal sealed class FileSystemWorkspaceSessionStore : IWorkspaceSessionStore
{
    private const string StoreDirectoryName = "Cognt";
    private const string StoreFileName = "workspace-sessions.json";
    private const int StoreVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string storePath;
    private readonly ILogger<FileSystemWorkspaceSessionStore> logger;

    public FileSystemWorkspaceSessionStore(ILogger<FileSystemWorkspaceSessionStore> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appDataPath))
        {
            appDataPath = Environment.CurrentDirectory;
        }

        storePath = Path.Combine(appDataPath, StoreDirectoryName, StoreFileName);
    }

    public WorkspaceSessionSnapshot Load(string collectionPath)
    {
        if (!TryNormalizePath(collectionPath, out string normalizedCollectionPath))
        {
            return new WorkspaceSessionSnapshot();
        }

        try
        {
            WorkspaceSessionStoreDocument store = LoadStore();
            WorkspaceSessionEntry? entry = store.Sessions.FirstOrDefault(candidate =>
                TryNormalizePath(candidate.CollectionPath, out string candidatePath)
                && string.Equals(candidatePath, normalizedCollectionPath, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                return new WorkspaceSessionSnapshot
                {
                    CollectionPath = normalizedCollectionPath,
                };
            }

            List<WorkspaceSessionNote> notes = [];
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (WorkspaceSessionNoteEntry note in entry.OpenNotes)
            {
                if (!TryNormalizePath(note.NotePath, out string notePath))
                {
                    continue;
                }

                if (!File.Exists(notePath))
                {
                    continue;
                }

                if (!IsPathInsideDirectory(notePath, normalizedCollectionPath))
                {
                    logger.LogWarning(
                        "Workspace session note skipped because it is outside the collection. CollectionPath={CollectionPath} NotePath={NotePath}",
                        normalizedCollectionPath,
                        notePath);
                    continue;
                }

                if (!seen.Add(notePath))
                {
                    continue;
                }

                notes.Add(new WorkspaceSessionNote
                {
                    NotePath = notePath,
                    Caret = NormalizeCaret(note.Caret),
                });
            }

            string? activeNotePath = TryNormalizePath(entry.ActiveNotePath, out string activePath)
                && notes.Any(note => string.Equals(note.NotePath, activePath, StringComparison.OrdinalIgnoreCase))
                    ? activePath
                    : notes.LastOrDefault()?.NotePath;


            return new WorkspaceSessionSnapshot
            {
                CollectionPath = normalizedCollectionPath,
                ActiveNotePath = activeNotePath,
                OpenNotes = notes,
            };
        }
        catch (Exception ex) when (IsRecoverableStoreException(ex))
        {
            logger.LogWarning(
                ex,
                "Workspace session load recovered from store failure. CollectionPath={CollectionPath} StorePath={StorePath}",
                normalizedCollectionPath,
                storePath);
            return new WorkspaceSessionSnapshot
            {
                CollectionPath = normalizedCollectionPath,
            };
        }
    }

    public void Save(WorkspaceSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!TryNormalizePath(snapshot.CollectionPath, out string normalizedCollectionPath))
        {
            return;
        }

        try
        {
            WorkspaceSessionStoreDocument store = LoadStore();
            List<WorkspaceSessionEntry> sessions = [.. store.Sessions.Where(candidate =>
                !TryNormalizePath(candidate.CollectionPath, out string candidatePath)
                || !string.Equals(candidatePath, normalizedCollectionPath, StringComparison.OrdinalIgnoreCase))];

            List<WorkspaceSessionNoteEntry> notes = [];
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (WorkspaceSessionNote note in snapshot.OpenNotes)
            {
                if (!TryNormalizePath(note.NotePath, out string notePath))
                {
                    continue;
                }

                if (!IsPathInsideDirectory(notePath, normalizedCollectionPath))
                {
                    logger.LogWarning(
                        "Workspace session save skipped note because it is outside the collection. CollectionPath={CollectionPath} NotePath={NotePath}",
                        normalizedCollectionPath,
                        notePath);
                    continue;
                }

                if (!seen.Add(notePath))
                {
                    continue;
                }

                notes.Add(new WorkspaceSessionNoteEntry
                {
                    NotePath = notePath,
                    Caret = NormalizeCaret(note.Caret),
                });
            }

            string? activeNotePath = TryNormalizePath(snapshot.ActiveNotePath, out string activePath)
                && notes.Any(note => string.Equals(note.NotePath, activePath, StringComparison.OrdinalIgnoreCase))
                    ? activePath
                    : notes.LastOrDefault()?.NotePath;

            sessions.Add(new WorkspaceSessionEntry
            {
                CollectionPath = normalizedCollectionPath,
                ActiveNotePath = activeNotePath,
                OpenNotes = notes,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            SaveStore(new WorkspaceSessionStoreDocument
            {
                Version = StoreVersion,
                Sessions = sessions
                    .OrderBy(session => session.CollectionPath, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            });
        }
        catch (Exception ex) when (IsRecoverableStoreException(ex))
        {
            logger.LogWarning(
                ex,
                "Workspace session save recovered from store failure. CollectionPath={CollectionPath} StorePath={StorePath}",
                normalizedCollectionPath,
                storePath);
        }
    }

    public void Forget(string collectionPath)
    {
        if (!TryNormalizePath(collectionPath, out string normalizedCollectionPath))
        {
            return;
        }

        try
        {

            WorkspaceSessionStoreDocument store = LoadStore();

            List<WorkspaceSessionEntry> remainingSessions = [.. store.Sessions.Where(candidate =>
                !TryNormalizePath(candidate.CollectionPath, out string candidatePath)
                || !string.Equals(candidatePath, normalizedCollectionPath, StringComparison.OrdinalIgnoreCase))];
            SaveStore(new WorkspaceSessionStoreDocument
            {
                Version = StoreVersion,
                Sessions = remainingSessions,
            });

        }
        catch (Exception ex) when (IsRecoverableStoreException(ex))
        {
            logger.LogWarning(
                ex,
                "Workspace session forget recovered from store failure. CollectionPath={CollectionPath} StorePath={StorePath}",
                normalizedCollectionPath,
                storePath);
        }
    }

    private WorkspaceSessionStoreDocument LoadStore()
    {
        if (!File.Exists(storePath))
        {
            return new WorkspaceSessionStoreDocument();
        }

        WorkspaceSessionStoreDocument? store = JsonSerializer.Deserialize<WorkspaceSessionStoreDocument>(
            File.ReadAllText(storePath),
            JsonOptions);
        if (store is null)
        {
            logger.LogWarning("Workspace session store deserialized to null. StorePath={StorePath}", storePath);
            return new WorkspaceSessionStoreDocument();
        }

        return store;
    }

    private void SaveStore(WorkspaceSessionStoreDocument store)
    {
        string directoryPath = Path.GetDirectoryName(storePath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(storePath, JsonSerializer.Serialize(store, JsonOptions));
    }

    private static EditorCaretState? NormalizeCaret(EditorCaretState? caret)
    {
        if (caret?.IsValid != true)
        {
            return null;
        }

        return new EditorCaretState
        {
            BlockId = caret.BlockId.Trim(),
            Index = Math.Max(0, caret.Index),
            Length = Math.Max(0, caret.Length),
        };
    }

    private static bool TryNormalizePath(string? path, out string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            normalizedPath = string.Empty;
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
            return true;
        }
        catch (Exception ex) when (IsRecoverableStoreException(ex))
        {
            normalizedPath = string.Empty;
            return false;
        }
    }

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        string normalizedPath = Path.GetFullPath(path);
        string normalizedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        return normalizedPath.Equals(normalizedDirectory, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(
                normalizedDirectory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(
                normalizedDirectory + Path.AltDirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableStoreException(Exception exception)
    {
        return exception is ArgumentException
            or IOException
            or JsonException
            or NotSupportedException
            or UnauthorizedAccessException;
    }

    private sealed class WorkspaceSessionStoreDocument
    {
        public int Version { get; init; } = StoreVersion;

        public List<WorkspaceSessionEntry> Sessions { get; init; } = [];
    }

    private sealed class WorkspaceSessionEntry
    {
        public string CollectionPath { get; init; } = string.Empty;

        public string? ActiveNotePath { get; init; }

        public List<WorkspaceSessionNoteEntry> OpenNotes { get; init; } = [];

        public DateTimeOffset UpdatedAt { get; init; }
    }

    private sealed class WorkspaceSessionNoteEntry
    {
        public string NotePath { get; init; } = string.Empty;

        public EditorCaretState? Caret { get; init; }
    }
}
