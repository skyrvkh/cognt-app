using System.Threading;
using System.Threading.Tasks;

namespace Cognt.Modules.PersonalKnowledgeManagement.Application.Safety;

/// <summary>
/// Public surface for collection safety operations: backup,
/// restore, trash, empty trash, and permanent deletion. The
/// application layer does not own a concrete filesystem or
/// archive implementation; the surface is bound at composition
/// time to executors that perform the work in the infrastructure
/// layer.
/// </summary>
public interface ICollectionSafetyService
{
    /// <summary>Starts a backup handle.</summary>
    BackupHandle StartBackup(BackupRequest request);

    /// <summary>Starts a restore handle.</summary>
    RestoreHandle StartRestore(RestoreRequest request);

    /// <summary>Starts a soft-delete (trash) handle.</summary>
    TrashHandle StartTrash(TrashRequest request);

    /// <summary>Starts a restore-from-trash handle.</summary>
    TrashRestoreHandle StartRestoreFromTrash(TrashRestoreRequest request);

    /// <summary>Starts an Empty Trash handle.</summary>
    EmptyTrashHandle StartEmptyTrash(EmptyTrashRequest request);

    /// <summary>Starts a Permanent Item Cleanup handle.</summary>
    PermanentDeleteHandle StartPermanentDelete(PermanentDeleteRequest request);
}

/// <summary>Request to create a backup of a single collection.</summary>
public sealed record BackupRequest(
    long CollectionId,
    string SourceCollectionRoot,
    string DestinationArchivePath,
    bool VerifyAfterWrite = true);

/// <summary>Result of a backup operation.</summary>
/// <param name="ArchivePath">Absolute path of the verified archive on success.</param>
/// <param name="FailureKind">Reason the backup failed; <see cref="BackupFailureKind.None"/> on success.</param>
public sealed record BackupOutcome(
    string? ArchivePath,
    BackupFailureKind FailureKind);

/// <summary>Reason a backup terminated without success. Stable.</summary>
public enum BackupFailureKind
{
    None = 0,
    Cancelled = 1,
    NotFound = 2,
    HostilePath = 3,
    DiskFull = 4,
    MissingAsset = 5,
    VerificationFailed = 6,
    Other = 99,
}

/// <summary>
/// Stage of an in-flight backup. Stable; new entries appended.
/// </summary>
public enum BackupStage
{
    None = 0,
    Validated = 1,
    SnapshotCreated = 2,
    ManifestWritten = 3,
    AssetsCopied = 4,
    ArchiveWritten = 5,
    Verified = 6,
    AtomicFinalized = 7,
}

/// <summary>
/// Sink for backup progress.
/// </summary>
public interface IBackupProgressSink
{
    void ReportStage(BackupStage stage);
    void ReportProgress(BackupStage stage, long completed, long total);
}

/// <summary>
/// No-op backup progress sink.
/// </summary>
public sealed class NullBackupProgressSink : IBackupProgressSink
{
    public static readonly NullBackupProgressSink Instance = new();
    private NullBackupProgressSink() { }
    public void ReportStage(BackupStage stage) { }
    public void ReportProgress(BackupStage stage, long completed, long total) { }
}

/// <summary>
/// Long-running backup handle. The caller observes the typed
/// progress sink and awaits <see cref="Completion"/>.
/// </summary>
public sealed class BackupHandle
{
    private readonly Task<BackupOutcome> work;

    /// <summary>Progress observer for the typed BackupStage enum.</summary>
    public sealed class BackupProgressObserver : IBackupProgressSink
    {
        private readonly List<IBackupProgressSink> subscribers = new();
        public void Subscribe(IBackupProgressSink sink) => subscribers.Add(sink);
        public void ReportStage(BackupStage stage)
        {
            foreach (var s in subscribers) s.ReportStage(stage);
        }
        public void ReportProgress(BackupStage stage, long completed, long total)
        {
            foreach (var s in subscribers) s.ReportProgress(stage, completed, total);
        }
    }

    internal BackupHandle(Task<BackupOutcome> work, BackupProgressObserver observer)
    {
        this.work = work;
        this.Observer = observer;
    }

    /// <summary>
    /// Builds a handle from a pre-completed task and a fresh
    /// progress observer. The constructor is public so test
    /// doubles can supply their own completion; production code
    /// uses the internal constructor wired by the safety
    /// service.
    /// </summary>
    public static BackupHandle FromCompletedTask(Task<BackupOutcome> work)
    {
        return new BackupHandle(work, new BackupProgressObserver());
    }

    /// <summary>The progress observer.</summary>
    public BackupProgressObserver Observer { get; }

    /// <summary>Final outcome task.</summary>
    public Task<BackupOutcome> Completion => work;
}

/// <summary>Request to restore a collection from a backup archive.</summary>
public sealed record RestoreRequest(
    long CollectionId,
    string ArchivePath,
    string TargetCollectionRoot,
    bool VerifyBeforeExtract = true);

/// <summary>Result of a restore operation.</summary>
/// <param name="FailureKind">Reason the restore failed; <see cref="RestoreFailureKind.None"/> on success.</param>
public sealed record RestoreOutcome(
    RestoreFailureKind FailureKind);

/// <summary>Reason a restore terminated without success. Stable.</summary>
public enum RestoreFailureKind
{
    None = 0,
    Cancelled = 1,
    ArchiveInvalid = 2,
    HostilePath = 3,
    DiskFull = 4,
    ChecksumMismatch = 5,
    SchemaInvalid = 6,
    Other = 99,
}

/// <summary>Stage of an in-flight restore. Stable.</summary>
public enum RestoreStage
{
    None = 0,
    Validated = 1,
    ManifestParsed = 2,
    PayloadExtracted = 3,
    DatabaseValidated = 4,
    Registered = 5,
}

/// <summary>Sink for restore progress.</summary>
public interface IRestoreProgressSink
{
    void ReportStage(RestoreStage stage);
    void ReportProgress(RestoreStage stage, long completed, long total);
}

/// <summary>No-op restore progress sink.</summary>
public sealed class NullRestoreProgressSink : IRestoreProgressSink
{
    public static readonly NullRestoreProgressSink Instance = new();
    private NullRestoreProgressSink() { }
    public void ReportStage(RestoreStage stage) { }
    public void ReportProgress(RestoreStage stage, long completed, long total) { }
}

/// <summary>Long-running restore handle.</summary>
public sealed class RestoreHandle
{
    /// <summary>Progress observer for the typed RestoreStage enum.</summary>
    public sealed class RestoreProgressObserver : IRestoreProgressSink
    {
        private readonly List<IRestoreProgressSink> subscribers = new();
        public void Subscribe(IRestoreProgressSink sink) => subscribers.Add(sink);
        public void ReportStage(RestoreStage stage)
        {
            foreach (var s in subscribers) s.ReportStage(stage);
        }
        public void ReportProgress(RestoreStage stage, long completed, long total)
        {
            foreach (var s in subscribers) s.ReportProgress(stage, completed, total);
        }
    }

    private readonly Task<RestoreOutcome> work;
    internal RestoreHandle(Task<RestoreOutcome> work, RestoreProgressObserver observer)
    {
        this.work = work;
        this.Observer = observer;
    }
    public RestoreProgressObserver Observer { get; }
    public Task<RestoreOutcome> Completion => work;
}

/// <summary>Long-running trash (soft-delete) handle.</summary>
public sealed class TrashHandle
{
    /// <summary>Progress observer for the typed TrashStage enum.</summary>
    public sealed class TrashProgressObserver : ITrashProgressSink
    {
        private readonly List<ITrashProgressSink> subscribers = new();
        public void Subscribe(ITrashProgressSink sink) => subscribers.Add(sink);
        public void ReportStage(TrashStage stage)
        {
            foreach (var s in subscribers) s.ReportStage(stage);
        }
        public void ReportProgress(TrashStage stage, long completed, long total)
        {
            foreach (var s in subscribers) s.ReportProgress(stage, completed, total);
        }
    }

    private readonly Task<TrashOutcome> work;
    internal TrashHandle(Task<TrashOutcome> work, TrashProgressObserver observer)
    {
        this.work = work;
        this.Observer = observer;
    }
    public TrashProgressObserver Observer { get; }
    public Task<TrashOutcome> Completion => work;
}

/// <summary>Long-running restore-from-trash handle.</summary>
public sealed class TrashRestoreHandle
{
    private readonly Task<TrashRestoreOutcome> work;
    internal TrashRestoreHandle(Task<TrashRestoreOutcome> work)
    {
        this.work = work;
    }
    public Task<TrashRestoreOutcome> Completion => work;
}

/// <summary>Long-running Empty Trash handle.</summary>
public sealed class EmptyTrashHandle
{
    /// <summary>Progress observer for the typed EmptyTrashStage enum.</summary>
    public sealed class EmptyTrashProgressObserver : IEmptyTrashProgressSink
    {
        private readonly List<IEmptyTrashProgressSink> subscribers = new();
        public void Subscribe(IEmptyTrashProgressSink sink) => subscribers.Add(sink);
        public void ReportStage(EmptyTrashStage stage)
        {
            foreach (var s in subscribers) s.ReportStage(stage);
        }
        public void ReportProgress(EmptyTrashStage stage, long completed, long total)
        {
            foreach (var s in subscribers) s.ReportProgress(stage, completed, total);
        }
    }

    private readonly Task<EmptyTrashOutcome> work;
    internal EmptyTrashHandle(Task<EmptyTrashOutcome> work, EmptyTrashProgressObserver observer)
    {
        this.work = work;
        this.Observer = observer;
    }
    public EmptyTrashProgressObserver Observer { get; }
    public Task<EmptyTrashOutcome> Completion => work;
}

/// <summary>Long-running Permanent Item Cleanup handle.</summary>
public sealed class PermanentDeleteHandle
{
    private readonly Task<PermanentDeleteOutcome> work;
    internal PermanentDeleteHandle(Task<PermanentDeleteOutcome> work)
    {
        this.work = work;
    }
    public Task<PermanentDeleteOutcome> Completion => work;
}
