using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cognt.Modules.PersonalKnowledgeManagement.Application.Safety;

/// <summary>
/// Single-owner facade that exposes the typed backup / restore /
/// trash surfaces without taking a hard dependency on the
/// infrastructure layer. The constructor takes executor
/// delegates; the composition root (PersonalKnowledgeManagementModule)
/// wires them to the concrete services that live in the
/// infrastructure layer.
/// </summary>
public sealed class CollectionSafetyService : ICollectionSafetyService
{
    private readonly Func<BackupRequest, CancellationToken, Task<BackupOutcome>> backupExecutor;
    private readonly Func<RestoreRequest, CancellationToken, Task<RestoreOutcome>> restoreExecutor;
    private readonly Func<TrashRequest, CancellationToken, Task<TrashOutcome>> trashExecutor;
    private readonly Func<TrashRestoreRequest, CancellationToken, Task<TrashRestoreOutcome>> trashRestoreExecutor;
    private readonly Func<EmptyTrashRequest, CancellationToken, Task<EmptyTrashOutcome>> emptyTrashExecutor;
    private readonly Func<PermanentDeleteRequest, CancellationToken, Task<PermanentDeleteOutcome>> permanentDeleteExecutor;

    /// <summary>
    /// Builds a facade from the six executor delegates. The
    /// application layer never sees the concrete service types.
    /// </summary>
    public CollectionSafetyService(
        Func<BackupRequest, CancellationToken, Task<BackupOutcome>> backupExecutor,
        Func<RestoreRequest, CancellationToken, Task<RestoreOutcome>> restoreExecutor,
        Func<TrashRequest, CancellationToken, Task<TrashOutcome>> trashExecutor,
        Func<TrashRestoreRequest, CancellationToken, Task<TrashRestoreOutcome>> trashRestoreExecutor,
        Func<EmptyTrashRequest, CancellationToken, Task<EmptyTrashOutcome>> emptyTrashExecutor,
        Func<PermanentDeleteRequest, CancellationToken, Task<PermanentDeleteOutcome>> permanentDeleteExecutor)
    {
        this.backupExecutor = backupExecutor ?? throw new ArgumentNullException(nameof(backupExecutor));
        this.restoreExecutor = restoreExecutor ?? throw new ArgumentNullException(nameof(restoreExecutor));
        this.trashExecutor = trashExecutor ?? throw new ArgumentNullException(nameof(trashExecutor));
        this.trashRestoreExecutor = trashRestoreExecutor ?? throw new ArgumentNullException(nameof(trashRestoreExecutor));
        this.emptyTrashExecutor = emptyTrashExecutor ?? throw new ArgumentNullException(nameof(emptyTrashExecutor));
        this.permanentDeleteExecutor = permanentDeleteExecutor ?? throw new ArgumentNullException(nameof(permanentDeleteExecutor));
    }

    /// <inheritdoc />
    public BackupHandle StartBackup(BackupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var observer = new BackupHandle.BackupProgressObserver();
        var work = Task.Run(() => backupExecutor(request, CancellationToken.None));
        return new BackupHandle(work, observer);
    }

    /// <inheritdoc />
    public RestoreHandle StartRestore(RestoreRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var observer = new RestoreHandle.RestoreProgressObserver();
        var work = Task.Run(() => restoreExecutor(request, CancellationToken.None));
        return new RestoreHandle(work, observer);
    }

    /// <inheritdoc />
    public TrashHandle StartTrash(TrashRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var observer = new TrashHandle.TrashProgressObserver();
        var work = Task.Run(() => trashExecutor(request, CancellationToken.None));
        return new TrashHandle(work, observer);
    }

    /// <inheritdoc />
    public TrashRestoreHandle StartRestoreFromTrash(TrashRestoreRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var work = Task.Run(() => trashRestoreExecutor(request, CancellationToken.None));
        return new TrashRestoreHandle(work);
    }

    /// <inheritdoc />
    public EmptyTrashHandle StartEmptyTrash(EmptyTrashRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var observer = new EmptyTrashHandle.EmptyTrashProgressObserver();
        var work = Task.Run(() => emptyTrashExecutor(request, CancellationToken.None));
        return new EmptyTrashHandle(work, observer);
    }

    /// <inheritdoc />
    public PermanentDeleteHandle StartPermanentDelete(PermanentDeleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var work = Task.Run(() => permanentDeleteExecutor(request, CancellationToken.None));
        return new PermanentDeleteHandle(work);
    }

    /// <summary>
    /// Runs the backup executor inline and maps cancellation to a
    /// typed failure. Used by adapters that want synchronous
    /// semantics without the Task wrapper.
    /// </summary>
    public static async Task<BackupOutcome> RunBackupExecutorAsync(
        Func<BackupRequest, CancellationToken, Task<BackupOutcome>> executor,
        BackupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await executor(request, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return new BackupOutcome(ArchivePath: null, FailureKind: BackupFailureKind.Cancelled);
        }
    }

    /// <summary>Same as <see cref="RunBackupExecutorAsync"/> for restore.</summary>
    public static async Task<RestoreOutcome> RunRestoreExecutorAsync(
        Func<RestoreRequest, CancellationToken, Task<RestoreOutcome>> executor,
        RestoreRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await executor(request, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return new RestoreOutcome(RestoreFailureKind.Cancelled);
        }
    }

    /// <summary>Same as <see cref="RunBackupExecutorAsync"/> for trash.</summary>
    public static async Task<TrashOutcome> RunTrashExecutorAsync(
        Func<TrashRequest, CancellationToken, Task<TrashOutcome>> executor,
        TrashRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await executor(request, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return new TrashOutcome(TrashStage.None, TrashFailureKind.Cancelled);
        }
    }

    /// <summary>Same as <see cref="RunBackupExecutorAsync"/> for empty trash.</summary>
    public static async Task<EmptyTrashOutcome> RunEmptyTrashExecutorAsync(
        Func<EmptyTrashRequest, CancellationToken, Task<EmptyTrashOutcome>> executor,
        EmptyTrashRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await executor(request, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return new EmptyTrashOutcome(
                SafetyBackup: new SafetyBackupVerification(false, null, null, "Cancelled"),
                Stage: EmptyTrashStage.None,
                FailureKind: EmptyTrashFailureKind.Cancelled,
                ItemsRemoved: 0);
        }
    }
}
