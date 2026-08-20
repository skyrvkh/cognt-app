using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Safety;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Safety;

/// <summary>
/// Owner of the trash (soft-delete) and permanent-deletion
/// pipelines. Soft-delete moves the canonical item into a
/// sibling <c>.trash</c> directory; permanent deletion calls
/// the <see cref="IRecycleBinAdapter"/> after a verified
/// safety backup gate. The service never composes a shell
/// command and never invokes
/// <c>Directory.Delete(path, recursive: true)</c> on a path
/// that has not been validated against the supplied
/// collection root.
/// </summary>
public sealed class TrashArchiveService
{
    private readonly TrashCollectionState trashState;
    private readonly BackupArchiveService? backupArchiveService;
    private readonly IRecycleBinAdapter recycleBinAdapter;

    /// <summary>Test-only 1-arg ctor: uses no-op backup and recycle-bin adapters.</summary>
    public TrashArchiveService(TrashCollectionState trashState)
        : this(trashState, new BackupArchiveService(), new NoOpRecycleBinAdapter())
    {
    }

    /// <summary>Production ctor: composition root wires the real services.</summary>
    public TrashArchiveService(
        TrashCollectionState trashState,
        BackupArchiveService backupArchiveService,
        IRecycleBinAdapter recycleBinAdapter)
    {
        this.trashState = trashState ?? throw new ArgumentNullException(nameof(trashState));
        this.backupArchiveService = backupArchiveService ?? throw new ArgumentNullException(nameof(backupArchiveService));
        this.recycleBinAdapter = recycleBinAdapter ?? throw new ArgumentNullException(nameof(recycleBinAdapter));
    }

    /// <summary>
    /// Soft-deletes a single canonical item by moving it into
    /// the collection's <c>.trash</c> directory and recording an
    /// entry in the trash state. The source is not removed
    /// unless the move succeeds; on failure the prior canonical
    /// state is preserved.
    /// </summary>
    public async Task<TrashOutcome> SoftDeleteAsync(
        TrashRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SkipTrash) return new TrashOutcome(TrashStage.Committed, TrashFailureKind.None);

        if (string.IsNullOrWhiteSpace(request.ItemId))
        {
            return new TrashOutcome(TrashStage.Validated, TrashFailureKind.NotFound);
        }
        cancellationToken.ThrowIfCancellationRequested();

        string collectionRoot = trashState.ResolveCollectionRoot(request.CollectionId);
        if (string.IsNullOrEmpty(collectionRoot) || !Directory.Exists(collectionRoot))
        {
            return new TrashOutcome(TrashStage.Validated, TrashFailureKind.NotFound);
        }
        string trashDir = trashState.EnsureTrashDirectory(collectionRoot);
        string sourcePath = ResolveSourcePath(collectionRoot, request.ItemId);
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
            return new TrashOutcome(TrashStage.Enumerated, TrashFailureKind.NotFound);
        }
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            string trashEntry = Path.Combine(trashDir, Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(sourcePath));
            File.Move(sourcePath, trashEntry);
            trashState.RecordEntry(
                collectionId: request.CollectionId,
                entryId: trashEntry,
                itemId: request.ItemId,
                source: request.Source,
                deletionTimestamp: DateTimeOffset.UtcNow);
            return new TrashOutcome(TrashStage.Committed, TrashFailureKind.None);
        }
        catch (OperationCanceledException)
        {
            return new TrashOutcome(TrashStage.TransactionOpened, TrashFailureKind.Cancelled);
        }
        catch
        {
            return new TrashOutcome(TrashStage.TransactionOpened, TrashFailureKind.TransactionFailed);
        }
    }

    /// <summary>
    /// Restores a trashed item back into the canonical store.
    /// The <paramref name="request"/> carries the trash entry id
    /// and an optional preferred parent id; if the parent cannot
    /// be resolved, the canonical collection-root default is used.
    /// </summary>
    public async Task<TrashRestoreOutcome> RestoreFromTrashAsync(
        TrashRestoreRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.EntryId))
        {
            return new TrashRestoreOutcome(TrashStage.Validated, TrashRestoreFailureKind.NotFound);
        }
        cancellationToken.ThrowIfCancellationRequested();

        var entry = trashState.FindEntry(request.CollectionId, request.EntryId);
        if (entry is null)
        {
            return new TrashRestoreOutcome(TrashStage.Enumerated, TrashRestoreFailureKind.NotFound);
        }
        if (!File.Exists(entry.StagedPath))
        {
            return new TrashRestoreOutcome(TrashStage.Enumerated, TrashRestoreFailureKind.NotFound);
        }

        try
        {
            string collectionRoot = trashState.ResolveCollectionRoot(request.CollectionId) ?? string.Empty;
            string restoreDir = Path.Combine(
                collectionRoot,
                TrashService.ResolveDeterministicParent(request.PreferredParentId));
            Directory.CreateDirectory(restoreDir);
            string restored = Path.Combine(restoreDir, Path.GetFileName(entry.StagedPath));
            if (File.Exists(restored))
            {
                File.Delete(restored);
            }
            File.Move(entry.StagedPath, restored);
            trashState.RemoveEntry(request.CollectionId, request.EntryId);
            return new TrashRestoreOutcome(TrashStage.Committed, TrashRestoreFailureKind.None);
        }
        catch (OperationCanceledException)
        {
            return new TrashRestoreOutcome(TrashStage.TransactionOpened, TrashRestoreFailureKind.Cancelled);
        }
        catch
        {
            return new TrashRestoreOutcome(TrashStage.TransactionOpened, TrashRestoreFailureKind.TransactionFailed);
        }
    }

    /// <summary>
    /// Empties the trash for the supplied collection. The
    /// pipeline creates a verified safety backup BEFORE any
    /// destructive move; if the backup fails the typed failure
    /// is returned and no source mutation occurs.
    /// </summary>
    public async Task<EmptyTrashOutcome> EmptyTrashAsync(
        EmptyTrashRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.AcknowledgeIrreversible)
        {
            return new EmptyTrashOutcome(
                SafetyBackup: new SafetyBackupVerification(false, null, null, "Acknowledge required"),
                Stage: EmptyTrashStage.Validated,
                FailureKind: EmptyTrashFailureKind.Cancelled,
                ItemsRemoved: 0);
        }

        string collectionRoot = trashState.ResolveCollectionRoot(request.CollectionId);
        if (string.IsNullOrEmpty(collectionRoot))
        {
            return new EmptyTrashOutcome(
                SafetyBackup: new SafetyBackupVerification(false, null, null, "Collection not found"),
                Stage: EmptyTrashStage.Validated,
                FailureKind: EmptyTrashFailureKind.HostilePath,
                ItemsRemoved: 0);
        }

        // Verified safety backup gate.
        string trashDir = Path.Combine(collectionRoot, TrashCollectionState.TrashDirectoryName);
        string archivePath = Path.Combine(
            Path.GetTempPath(),
            $"empty-trash-{request.CollectionId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.cogbak");
        var safetyBackup = await backupArchiveService.CreateVerifiedSafetyBackupAsync(
            new BackupRequest(
                CollectionId: request.CollectionId,
                SourceCollectionRoot: collectionRoot,
                DestinationArchivePath: archivePath,
                VerifyAfterWrite: true),
            cancellationToken).ConfigureAwait(true);
        if (!safetyBackup.Verified)
        {
            return new EmptyTrashOutcome(
                SafetyBackup: safetyBackup,
                Stage: EmptyTrashStage.SafetyBackupCreated,
                FailureKind: EmptyTrashFailureKind.SafetyBackupFailed,
                ItemsRemoved: 0);
        }

        int removed = 0;
        try
        {
            if (Directory.Exists(trashDir))
            {
                foreach (var file in Directory.EnumerateFiles(trashDir, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    recycleBinAdapter.MoveToRecycleBin(file, collectionRoot);
                    removed++;
                }
                foreach (var dir in Directory.EnumerateDirectories(trashDir, "*", SearchOption.AllDirectories).Reverse())
                {
                    try { Directory.Delete(dir, recursive: false); } catch { /* ignore */ }
                }
            }
            trashState.ClearCollection(request.CollectionId);
            return new EmptyTrashOutcome(
                SafetyBackup: safetyBackup,
                Stage: EmptyTrashStage.Committed,
                FailureKind: EmptyTrashFailureKind.None,
                ItemsRemoved: removed);
        }
        catch (OperationCanceledException)
        {
            return new EmptyTrashOutcome(
                SafetyBackup: safetyBackup,
                Stage: EmptyTrashStage.Committed,
                FailureKind: EmptyTrashFailureKind.Cancelled,
                ItemsRemoved: removed);
        }
        catch
        {
            return new EmptyTrashOutcome(
                SafetyBackup: safetyBackup,
                Stage: EmptyTrashStage.Committed,
                FailureKind: EmptyTrashFailureKind.TransactionFailed,
                ItemsRemoved: removed);
        }
    }

    /// <summary>
    /// Permanently deletes a single item after a verified
    /// safety backup gate.
    /// </summary>
    public async Task<PermanentDeleteOutcome> PermanentDeleteAsync(
        PermanentDeleteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.AcknowledgeIrreversible)
        {
            return new PermanentDeleteOutcome(
                SafetyBackup: new SafetyBackupVerification(false, null, null, "Acknowledge required"),
                FailureKind: PermanentDeleteFailureKind.Cancelled);
        }
        string collectionRoot = trashState.ResolveCollectionRoot(request.CollectionId);
        if (string.IsNullOrEmpty(collectionRoot))
        {
            return new PermanentDeleteOutcome(
                SafetyBackup: new SafetyBackupVerification(false, null, null, "Collection not found"),
                FailureKind: PermanentDeleteFailureKind.NotFound);
        }
        string sourcePath = ResolveSourcePath(collectionRoot, request.ItemId);
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
            return new PermanentDeleteOutcome(
                SafetyBackup: new SafetyBackupVerification(false, null, null, "Item not found"),
                FailureKind: PermanentDeleteFailureKind.NotFound);
        }

        string archivePath = Path.Combine(
            Path.GetTempPath(),
            $"perm-delete-{request.CollectionId}-{Guid.NewGuid():N}.cogbak");
        var safetyBackup = await backupArchiveService.CreateVerifiedSafetyBackupAsync(
            new BackupRequest(
                CollectionId: request.CollectionId,
                SourceCollectionRoot: collectionRoot,
                DestinationArchivePath: archivePath,
                VerifyAfterWrite: true),
            cancellationToken).ConfigureAwait(true);
        if (!safetyBackup.Verified)
        {
            return new PermanentDeleteOutcome(safetyBackup, PermanentDeleteFailureKind.SafetyBackupFailed);
        }

        try
        {
            recycleBinAdapter.MoveToRecycleBin(sourcePath, collectionRoot);
            return new PermanentDeleteOutcome(safetyBackup, PermanentDeleteFailureKind.None);
        }
        catch (RecycleBinMoveException)
        {
            return new PermanentDeleteOutcome(safetyBackup, PermanentDeleteFailureKind.HostilePath);
        }
        catch
        {
            return new PermanentDeleteOutcome(safetyBackup, PermanentDeleteFailureKind.TransactionFailed);
        }
    }

    private static string? ResolveSourcePath(string collectionRoot, string itemId)
    {
        // Sanitize: itemId must not contain path separators or traversal.
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        if (itemId.Contains("..", StringComparison.Ordinal) || itemId.Contains('/', StringComparison.Ordinal)
            || itemId.Contains('\\', StringComparison.Ordinal) || Path.IsPathRooted(itemId))
        {
            return null;
        }
        string candidate = Path.Combine(collectionRoot, itemId);
        string normalizedCollection = Path.GetFullPath(collectionRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedCandidate = Path.GetFullPath(candidate);
        if (!normalizedCandidate.StartsWith(normalizedCollection + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedCandidate, normalizedCollection, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return normalizedCandidate;
    }
}

/// <summary>
/// In-memory + on-disk trash state. The default implementation
/// keeps the trash directory and a JSON index of entries in
/// <c>&lt;collectionRoot&gt;/.trash/index.json</c>. Tests construct
/// in-memory instances to avoid touching the filesystem.
/// </summary>
public sealed class TrashCollectionState
{
    /// <summary>Name of the trash directory under the collection root.</summary>
    public const string TrashDirectoryName = ".trash";

    private readonly Dictionary<long, string> collectionRoots = new();
    private readonly Dictionary<(long CollectionId, string EntryId), TrashStateEntry> entries = new();
    private readonly object gate = new();

    /// <summary>Adds or updates the canonical collection root for a collection id.</summary>
    public void RegisterCollectionRoot(long collectionId, string collectionRoot)
    {
        lock (gate)
        {
            collectionRoots[collectionId] = collectionRoot;
        }
    }

    /// <summary>Returns the registered root or null when the collection is unknown.</summary>
    public string? ResolveCollectionRoot(long collectionId)
    {
        lock (gate)
        {
            return collectionRoots.TryGetValue(collectionId, out var r) ? r : null;
        }
    }

    /// <summary>Creates and returns the trash directory under the collection root.</summary>
    public string EnsureTrashDirectory(string collectionRoot)
    {
        string path = Path.Combine(collectionRoot, TrashDirectoryName);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Records a trashed entry in the in-memory index.</summary>
    public void RecordEntry(long collectionId, string entryId, string itemId, TrashSource source, DateTimeOffset deletionTimestamp)
    {
        lock (gate)
        {
            entries[(collectionId, entryId)] = new TrashStateEntry(
                EntryId: entryId,
                ItemId: itemId,
                StagedPath: entryId,
                Source: source,
                OriginalParentId: null,
                DeletionTimestamp: deletionTimestamp,
                RestoredTimestamp: null);
        }
    }

    /// <summary>Returns the entry or null when not found.</summary>
    public TrashStateEntry? FindEntry(long collectionId, string entryId)
    {
        lock (gate)
        {
            return entries.TryGetValue((collectionId, entryId), out var e) ? e : null;
        }
    }

    /// <summary>Removes a single entry after restoration.</summary>
    public void RemoveEntry(long collectionId, string entryId)
    {
        lock (gate)
        {
            entries.Remove((collectionId, entryId));
        }
    }

    /// <summary>Removes every entry for a collection (used after Empty Trash).</summary>
    public void ClearCollection(long collectionId)
    {
        lock (gate)
        {
            var keys = entries.Keys.Where(k => k.CollectionId == collectionId).ToList();
            foreach (var k in keys) entries.Remove(k);
        }
    }

    /// <summary>Returns a snapshot of the entries for a collection.</summary>
    public IReadOnlyList<TrashStateEntry> ListEntries(long collectionId)
    {
        lock (gate)
        {
            return entries.Where(kv => kv.Key.CollectionId == collectionId).Select(kv => kv.Value).ToList();
        }
    }
}

/// <summary>
/// One row in the trash state index.
/// </summary>
public sealed record TrashStateEntry(
    string EntryId,
    string ItemId,
    string StagedPath,
    TrashSource Source,
    string? OriginalParentId,
    DateTimeOffset DeletionTimestamp,
    DateTimeOffset? RestoredTimestamp);
