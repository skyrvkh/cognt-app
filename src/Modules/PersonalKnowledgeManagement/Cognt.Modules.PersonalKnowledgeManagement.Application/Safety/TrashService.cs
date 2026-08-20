using System.Threading;
using System.Threading.Tasks;

namespace Cognt.Modules.PersonalKnowledgeManagement.Application.Safety;

/// <summary>
/// Stage of an in-flight trash (soft-delete) operation. Stages
/// are reported through <see cref="ITrashProgressSink"/> and
/// mirrored on <see cref="TrashOutcome.Stage"/>. Values are
/// stable; new entries are appended to the end of the list so
/// existing persisted state is never re-mapped.
/// </summary>
public enum TrashStage
{
    /// <summary>No work has been performed yet.</summary>
    None = 0,

    /// <summary>The collection root and target item have been validated.</summary>
    Validated = 1,

    /// <summary>The item and its descendants have been enumerated.</summary>
    Enumerated = 2,

    /// <summary>The soft-delete transaction has been opened.</summary>
    TransactionOpened = 3,

    /// <summary>The soft-delete transaction has committed.</summary>
    Committed = 4,
}

/// <summary>
/// Reason a trash (soft-delete) operation terminated without
/// success. Values are stable; new entries are appended to the
/// end of the list.
/// </summary>
public enum TrashFailureKind
{
    /// <summary>No failure has been recorded.</summary>
    None = 0,
    /// <summary>The caller cancelled the operation.</summary>
    Cancelled = 1,
    /// <summary>The target item was not found in canonical state.</summary>
    NotFound = 2,
    /// <summary>The target item is already in Trash.</summary>
    AlreadyTrashed = 3,
    /// <summary>The source path violated the containment / safety contract.</summary>
    HostilePath = 4,
    /// <summary>The trash transaction failed; the prior state was rolled back.</summary>
    TransactionFailed = 5,
    /// <summary>Any other failure.</summary>
    Other = 99,
}

/// <summary>
/// Sink for trash (soft-delete) progress. The application layer
/// uses this interface to project progress onto a view-model
/// without coupling the infrastructure layer to WPF types.
/// </summary>
public interface ITrashProgressSink
{
    /// <summary>Reports that the operation has moved into <paramref name="stage"/>.</summary>
    void ReportStage(TrashStage stage);

    /// <summary>Reports determinate progress of the current stage.</summary>
    void ReportProgress(TrashStage stage, long completed, long total);
}

/// <summary>
/// No-op trash progress sink. The single Instance is reused to
/// avoid per-call allocation.
/// </summary>
public sealed class NullTrashProgressSink : ITrashProgressSink
{
    /// <summary>Singleton instance.</summary>
    public static readonly NullTrashProgressSink Instance = new();

    private NullTrashProgressSink()
    {
    }

    /// <inheritdoc />
    public void ReportStage(TrashStage stage)
    {
    }

    /// <inheritdoc />
    public void ReportProgress(TrashStage stage, long completed, long total)
    {
    }
}

/// <summary>
/// Request to soft-delete a collection item into Trash.
/// </summary>
public sealed record TrashRequest(
    long CollectionId,
    string ItemId,
    TrashSource Source,
    bool SkipTrash = false);

/// <summary>
/// Result of a trash (soft-delete) operation.
/// </summary>
/// <param name="Stage">Stage the operation ended at.</param>
/// <param name="FailureKind">Reason the operation failed; <see cref="TrashFailureKind.None"/> on success.</param>
public sealed record TrashOutcome(
    TrashStage Stage,
    TrashFailureKind FailureKind);

/// <summary>
/// Source of a trashed item (which surface produced it).
/// </summary>
public enum TrashSource
{
    Note = 0,
    Folder = 1,
    Flashcard = 2,
    Other = 99,
}

/// <summary>
/// Reason a restore-from-trash operation terminated without
/// success. Stable; new entries appended to the end.
/// </summary>
public enum TrashRestoreFailureKind
{
    /// <summary>No failure recorded.</summary>
    None = 0,
    /// <summary>The caller cancelled.</summary>
    Cancelled = 1,
    /// <summary>The trash entry was not found.</summary>
    NotFound = 2,
    /// <summary>Parent could not be resolved and no fallback exists.</summary>
    ParentMissing = 3,
    /// <summary>The path violated the containment / safety contract.</summary>
    HostilePath = 4,
    /// <summary>The transaction failed; the prior state was rolled back.</summary>
    TransactionFailed = 5,
    /// <summary>Any other failure.</summary>
    Other = 99,
}

/// <summary>
/// Request to restore a trashed item to the canonical store.
/// </summary>
public sealed record TrashRestoreRequest(
    long CollectionId,
    string EntryId,
    TrashSource Source,
    string? PreferredParentId = null);

/// <summary>
/// Result of a restore-from-trash operation.
/// </summary>
public sealed record TrashRestoreOutcome(
    TrashStage Stage,
    TrashRestoreFailureKind FailureKind);

/// <summary>
/// Stage of an in-flight Empty Trash (permanent deletion) operation.
/// Stable; new entries appended to the end.
/// </summary>
public enum EmptyTrashStage
{
    None = 0,
    Validated = 1,
    Enumerated = 2,
    SafetyBackupCreated = 3,
    TransactionOpened = 4,
    Committed = 5,
}

/// <summary>
/// Reason Empty Trash terminated without success. Stable; new
/// entries appended to the end.
/// </summary>
public enum EmptyTrashFailureKind
{
    None = 0,
    /// <summary>The caller cancelled.</summary>
    Cancelled = 1,
    /// <summary>The path violated the containment contract.</summary>
    HostilePath = 2,
    /// <summary>The verified safety backup could not be created.</summary>
    SafetyBackupFailed = 3,
    /// <summary>The trash transaction failed.</summary>
    TransactionFailed = 4,
    /// <summary>Any other failure.</summary>
    Other = 99,
}

/// <summary>
/// Sink for Empty Trash progress.
/// </summary>
public interface IEmptyTrashProgressSink
{
    void ReportStage(EmptyTrashStage stage);
    void ReportProgress(EmptyTrashStage stage, long completed, long total);
}

/// <summary>
/// No-op Empty Trash progress sink.
/// </summary>
public sealed class NullEmptyTrashProgressSink : IEmptyTrashProgressSink
{
    public static readonly NullEmptyTrashProgressSink Instance = new();
    private NullEmptyTrashProgressSink() { }
    public void ReportStage(EmptyTrashStage stage) { }
    public void ReportProgress(EmptyTrashStage stage, long completed, long total) { }
}

/// <summary>
/// Result of a verified safety backup gate.
/// </summary>
/// <param name="Verified">Whether the verified backup succeeded.</param>
/// <param name="ArchivePath">Absolute path of the verified archive, or null on failure.</param>
/// <param name="ManifestSha256">Lowercase hex SHA-256 of the verified manifest.</param>
/// <param name="FailureReason">Single-line failure reason; null on success.</param>
public sealed record SafetyBackupVerification(
    bool Verified,
    string? ArchivePath,
    string? ManifestSha256,
    string? FailureReason);

/// <summary>
/// Request to empty all trashed items in a collection.
/// </summary>
public sealed record EmptyTrashRequest(
    long CollectionId,
    bool AcknowledgeIrreversible);

/// <summary>
/// Result of an Empty Trash operation.
/// </summary>
public sealed record EmptyTrashOutcome(
    SafetyBackupVerification SafetyBackup,
    EmptyTrashStage Stage,
    EmptyTrashFailureKind FailureKind,
    int ItemsRemoved);

/// <summary>
/// Reason a Permanent Item Cleanup terminated without success.
/// </summary>
public enum PermanentDeleteFailureKind
{
    None = 0,
    Cancelled = 1,
    NotFound = 2,
    HostilePath = 3,
    SafetyBackupFailed = 4,
    SharedAssetInUse = 5,
    TransactionFailed = 6,
    Other = 99,
}

/// <summary>
/// Request to permanently delete a single item.
/// </summary>
public sealed record PermanentDeleteRequest(
    long CollectionId,
    string ItemId,
    TrashSource Source,
    bool AcknowledgeIrreversible);

/// <summary>
/// Result of a Permanent Item Cleanup.
/// </summary>
public sealed record PermanentDeleteOutcome(
    SafetyBackupVerification SafetyBackup,
    PermanentDeleteFailureKind FailureKind);

/// <summary>
/// Static contract-host for the trash/permanent-deletion surface.
/// The class is the canonical owner of the closed enumerations
/// and the typed request / outcome records; infrastructure
/// services implement the contract without inheriting from it.
/// </summary>
public static class TrashService
{
    /// <summary>Canonical default parent id used when the parent cannot be resolved.</summary>
    public const string DefaultParentId = "collection-root";

    /// <summary>
    /// Resolves the deterministic parent id from an explicit
    /// preferred id. Returns the canonical default when the
    /// preferred id is null, empty, or whitespace.
    /// </summary>
    public static string ResolveDeterministicParent(string? preferredParentId)
        => string.IsNullOrWhiteSpace(preferredParentId) ? DefaultParentId : preferredParentId!;

    /// <summary>True when <paramref name="parentId"/> is the collection-root default.</summary>
    public static bool IsCollectionRootParent(string? parentId)
        => string.IsNullOrWhiteSpace(parentId) || parentId == DefaultParentId;

    /// <summary>True when the two parents refer to the same target.</summary>
    public static bool AreParentsEqual(string? left, string? right)
        => ResolveDeterministicParent(left) == ResolveDeterministicParent(right);
}
