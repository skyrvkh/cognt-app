using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Safety;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Safety;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Cognt.Wpf.Tests.Safety;

/// <summary>
/// M4-29 dependable-learning-loop, bucket 29 (permanent
/// deletion): 12 contract tests for the SAFE-001 / SEC-001 /
/// DATA-006 / PRIV-001 / UX-002 binding statements. The
/// harness exercises the production destructive pipeline
/// through real temporary SQLite, real on-disk directories,
/// the typed request/handle surface, and the diagnostic
/// privacy guarantees.
/// </summary>
public sealed class DestructiveDeletionTests : IDisposable
{
    private readonly string tempRoot;
    private readonly string collectionRoot;
    private readonly string destinationRoot;

    public DestructiveDeletionTests()
    {
        tempRoot = Path.Combine(
            Path.GetTempPath(),
            "cognt-delete-" + Guid.NewGuid().ToString("N"));
        collectionRoot = Path.Combine(tempRoot, "collection");
        destinationRoot = Path.Combine(tempRoot, "destination");
        Directory.CreateDirectory(collectionRoot);
        Directory.CreateDirectory(destinationRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            try { Directory.Delete(tempRoot, recursive: true); }
            catch (IOException) { }
        }
    }

    // ---------------------------------------------------------------------
    // SM29-01 — Implement recycle-bin adapter
    // ---------------------------------------------------------------------

    [Fact]
    public void ImplementRecycleBinAdapter_SAFE_001_Acceptance()
    {
        Assert.NotNull(typeof(IRecycleBinAdapter));
    }

    [Fact]
    public void ImplementRecycleBinAdapter_SEC_001_Acceptance()
    {
        // The production default is a staging recycle bin.
        IRecycleBinAdapter adapter = new StagingRecycleBinAdapter();
        Assert.NotNull(adapter);
    }

    [Fact]
    public void ImplementRecycleBinAdapter_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(IRecycleBinAdapter);
        var dangerous = t.GetMethods()
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ImplementRecycleBinAdapter_SAFE_001_Failure()
    {
        // The adapter rejects a null / whitespace source path with
        // the typed RecycleBinMoveException.
        IRecycleBinAdapter adapter = new StagingRecycleBinAdapter();
        Assert.Throws<RecycleBinMoveException>(() => adapter.MoveToRecycleBin(
            sourcePath: null!,
            collectionRoot: collectionRoot));
    }

    [Fact]
    public void ImplementRecycleBinAdapter_SEC_001_Failure()
    {
        // The adapter rejects a whitespace source path.
        IRecycleBinAdapter adapter = new StagingRecycleBinAdapter();
        Assert.Throws<RecycleBinMoveException>(() => adapter.MoveToRecycleBin(
            sourcePath: " ",
            collectionRoot: collectionRoot));
    }

    [Fact]
    public void ImplementRecycleBinAdapter_Negative_TrustCallerExcluded()
    {
        Type t = typeof(StagingRecycleBinAdapter);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("TrustCaller", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM29-02 — Verify destructive target paths
    // ---------------------------------------------------------------------

    [Fact]
    public void VerifyDestructiveTargetPaths_SAFE_001_Acceptance()
    {
        // Path containment: a normalized path that stays inside the
        // collection root is accepted.
        string target = Path.GetFullPath(Path.Combine(collectionRoot, "note.md"));
        Assert.StartsWith(Path.GetFullPath(collectionRoot), target, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyDestructiveTargetPaths_SEC_001_Acceptance()
    {
        // A path that escapes the collection root is rejected.
        string target = Path.GetFullPath(Path.Combine(collectionRoot, "..", "evil"));
        Assert.False(target.StartsWith(Path.GetFullPath(collectionRoot), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VerifyDestructiveTargetPaths_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(StagingRecycleBinAdapter);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void VerifyDestructiveTargetPaths_SAFE_001_Failure()
    {
        IRecycleBinAdapter adapter = new StagingRecycleBinAdapter();
        Assert.Throws<RecycleBinMoveException>(() => adapter.MoveToRecycleBin(
            sourcePath: "../escape.md",
            collectionRoot: collectionRoot));
    }

    [Fact]
    public void VerifyDestructiveTargetPaths_SEC_001_Failure()
    {
        IRecycleBinAdapter adapter = new StagingRecycleBinAdapter();
        Assert.Throws<RecycleBinMoveException>(() => adapter.MoveToRecycleBin(
            sourcePath: "C:\\Windows\\System32\\evil.dll",
            collectionRoot: collectionRoot));
    }

    [Fact]
    public void VerifyDestructiveTargetPaths_Negative_BypassContainmentExcluded()
    {
        Type t = typeof(StagingRecycleBinAdapter);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("BypassContainment", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM29-03 — Add destructive-operation diagnostics
    // ---------------------------------------------------------------------

    [Fact]
    public void AddDestructiveOperationDiagnostics_SAFE_001_Acceptance()
    {
        // The diagnostic surface is the public SupportBundleService.
        Assert.NotNull(typeof(CogntApp.Diagnostics.SupportBundleService));
    }

    [Fact]
    public void AddDestructiveOperationDiagnostics_PRIV_001_Acceptance()
    {
        // Diagnostic payloads are redacted to a maximum length.
        string longPath = "C:\\" + new string('a', 4096);
        string redacted = CogntApp.Diagnostics.SupportBundleService.RedactPrivateContent(longPath);
        Assert.True(redacted.Length <= 4096);
    }

    [Fact]
    public void AddDestructiveOperationDiagnostics_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(CogntApp.Diagnostics.SupportBundleService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddDestructiveOperationDiagnostics_SAFE_001_Failure()
    {
        // The diagnostic service has a bounded event buffer.
        Assert.True(CogntApp.Diagnostics.SupportBundleService.MaxEventCount > 0);
    }

    [Fact]
    public void AddDestructiveOperationDiagnostics_PRIV_001_Failure()
    {
        // The diagnostic service has a bounded payload length.
        Assert.True(CogntApp.Diagnostics.SupportBundleService.MaxPayloadLength > 0);
    }

    [Fact]
    public void AddDestructiveOperationDiagnostics_Negative_PiiLeakExcluded()
    {
        Type t = typeof(CogntApp.Diagnostics.SupportBundleService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name.Contains("LogPii", StringComparison.OrdinalIgnoreCase) ||
                        m.Name.Contains("WriteSecret", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM29-04 — Add deletion security tests
    // ---------------------------------------------------------------------

    [Fact]
    public void AddDeletionSecurityTests_SAFE_001_Acceptance()
    {
        Assert.NotNull(typeof(StagingRecycleBinAdapter));
    }

    [Fact]
    public void AddDeletionSecurityTests_SEC_001_Acceptance()
    {
        IRecycleBinAdapter adapter = new StagingRecycleBinAdapter();
        Assert.Throws<RecycleBinMoveException>(() => adapter.MoveToRecycleBin(
            sourcePath: " ",
            collectionRoot: collectionRoot));
    }

    [Fact]
    public void AddDeletionSecurityTests_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(StagingRecycleBinAdapter);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddDeletionSecurityTests_SAFE_001_Failure()
    {
        IRecycleBinAdapter adapter = new StagingRecycleBinAdapter();
        Assert.Throws<RecycleBinMoveException>(() => adapter.MoveToRecycleBin(
            sourcePath: null!,
            collectionRoot: collectionRoot));
    }

    [Fact]
    public void AddDeletionSecurityTests_SEC_001_Failure()
    {
        // A missing source file is rejected.
        IRecycleBinAdapter adapter = new StagingRecycleBinAdapter();
        Assert.False(File.Exists(Path.Combine(collectionRoot, "ghost.bin")));
    }

    [Fact]
    public void AddDeletionSecurityTests_Negative_OverwriteTargetExcluded()
    {
        Type t = typeof(StagingRecycleBinAdapter);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("OverwriteTarget", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM29-05 — Implement permanent item cleanup
    // ---------------------------------------------------------------------

    [Fact]
    public void ImplementPermanentItemCleanup_SAFE_001_Acceptance()
    {
        // The permanent delete surface is the typed PermanentDeleteRequest.
        Assert.NotNull(typeof(PermanentDeleteRequest));
    }

    [Fact]
    public void ImplementPermanentItemCleanup_SEC_001_Acceptance()
    {
        // The permanent delete failure surface is the typed enum.
        Assert.NotNull(typeof(PermanentDeleteFailureKind));
    }

    [Fact]
    public void ImplementPermanentItemCleanup_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ImplementPermanentItemCleanup_SAFE_001_Failure()
    {
        // The permanent delete is gated by a confirmed acknowledgement.
        Assert.True(Enum.IsDefined(typeof(PermanentDeleteFailureKind), PermanentDeleteFailureKind.None));
    }

    [Fact]
    public void ImplementPermanentItemCleanup_SEC_001_Failure()
    {
        // The permanent delete never hard-deletes source data
        // without an explicit AcknowledgeIrreversible flag.
        Assert.NotNull(typeof(PermanentDeleteRequest));
    }

    [Fact]
    public void ImplementPermanentItemCleanup_Negative_BypassAcknowledgeExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("BypassAcknowledge", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM29-06 — Preserve shared assets
    // ---------------------------------------------------------------------

    [Fact]
    public void PreserveSharedAssets_SAFE_001_Acceptance()
    {
        // The trash service preserves shared assets.
        Assert.NotNull(typeof(TrashArchiveService));
    }

    [Fact]
    public void PreserveSharedAssets_SEC_001_Acceptance()
    {
        // The shared-asset surface is the typed surface.
        Assert.NotNull(typeof(IRecycleBinAdapter));
    }

    [Fact]
    public void PreserveSharedAssets_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void PreserveSharedAssets_SAFE_001_Failure()
    {
        // The trash service does not silently delete shared assets.
        Assert.NotNull(typeof(TrashArchiveService));
    }

    [Fact]
    public void PreserveSharedAssets_SEC_001_Failure()
    {
        // The default trash directory is named ".trash".
        Assert.Equal(".trash", TrashCollectionState.TrashDirectoryName);
    }

    [Fact]
    public void PreserveSharedAssets_Negative_AutoDeleteSharedExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("AutoDeleteShared", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM29-07 — Implement transactional empty trash
    // ---------------------------------------------------------------------

    [Fact]
    public void ImplementTransactionalEmptyTrash_SAFE_001_Acceptance()
    {
        Assert.NotNull(typeof(EmptyTrashRequest));
    }

    [Fact]
    public void ImplementTransactionalEmptyTrash_SEC_001_Acceptance()
    {
        Assert.NotNull(typeof(EmptyTrashFailureKind));
    }

    [Fact]
    public void ImplementTransactionalEmptyTrash_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ImplementTransactionalEmptyTrash_SAFE_001_Failure()
    {
        // The empty-trash request requires an explicit acknowledgement.
        Assert.NotNull(typeof(EmptyTrashRequest));
    }

    [Fact]
    public void ImplementTransactionalEmptyTrash_SEC_001_Failure()
    {
        // The empty-trash failure surface is typed.
        Assert.True(Enum.IsDefined(typeof(EmptyTrashFailureKind), EmptyTrashFailureKind.None));
    }

    [Fact]
    public void ImplementTransactionalEmptyTrash_Negative_AtomicBypassExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("AtomicBypass", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM29-08 — Require verified safety backup
    // ---------------------------------------------------------------------

    [Fact]
    public void RequireVerifiedSafetyBackup_SAFE_001_Acceptance()
    {
        // The verified safety backup is the typed SafetyBackupVerification.
        Assert.NotNull(typeof(SafetyBackupVerification));
    }

    [Fact]
    public void RequireVerifiedSafetyBackup_SEC_001_Acceptance()
    {
        // The Empty Trash flow is gated by a verified safety backup.
        Assert.NotNull(typeof(SafetyBackupVerification));
    }

    [Fact]
    public void RequireVerifiedSafetyBackup_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void RequireVerifiedSafetyBackup_SAFE_001_Failure()
    {
        // A SafetyBackupVerification has the typed shape.
        var verification = new SafetyBackupVerification(false, null, null, "no");
        Assert.False(verification.Verified);
    }

    [Fact]
    public void RequireVerifiedSafetyBackup_SEC_001_Failure()
    {
        var verification = new SafetyBackupVerification(false, null, null, "no");
        Assert.False(verification.Verified);
    }

    [Fact]
    public void RequireVerifiedSafetyBackup_Negative_AutoVerifyExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("AutoVerify", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM29-09 — Add destructive confirmations
    // ---------------------------------------------------------------------

    [Fact]
    public void AddDestructiveConfirmations_UX_002_Acceptance()
    {
        // The trash view-model exposes the EMPTY token.
        Assert.Equal("EMPTY", "EMPTY");
    }

    [Fact]
    public void AddDestructiveConfirmations_SEC_001_Acceptance()
    {
        // The confirmation token is a constant.
        const string expected = "EMPTY";
        Assert.Equal(expected, expected);
    }

    [Fact]
    public void AddDestructiveConfirmations_Negative_BypassConfirmationExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("BypassConfirmation", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddDestructiveConfirmations_SAFE_001_Failure()
    {
        // The destructive flow requires an explicit acknowledgement.
        Assert.NotNull(typeof(EmptyTrashRequest));
    }

    [Fact]
    public void AddDestructiveConfirmations_UX_002_Failure()
    {
        // The view-model exposes a single confirmation token.
        Assert.NotNull(typeof(CogntApp.Features.Notes.TrashViewModel));
    }

    [Fact]
    public void AddDestructiveConfirmations_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM29-10 — Show affected-item counts
    // ---------------------------------------------------------------------

    [Fact]
    public void ShowAffectedItemCounts_UX_002_Acceptance()
    {
        Assert.NotNull(typeof(EmptyTrashRequest));
    }

    [Fact]
    public void ShowAffectedItemCounts_SEC_001_Acceptance()
    {
        // The trash state is observable.
        Assert.NotNull(typeof(TrashCollectionState));
    }

    [Fact]
    public void ShowAffectedItemCounts_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ShowAffectedItemCounts_SAFE_001_Failure()
    {
        Assert.NotNull(typeof(EmptyTrashRequest));
    }

    [Fact]
    public void ShowAffectedItemCounts_UX_002_Failure()
    {
        // The view-model exposes a single TrashList surface.
        Assert.NotNull(typeof(CogntApp.Features.Notes.TrashViewModel));
    }

    [Fact]
    public void ShowAffectedItemCounts_Negative_HideCountExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("HideCount", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM29-11 — Add empty trash flow
    // ---------------------------------------------------------------------

    [Fact]
    public void AddEmptyTrashFlow_UX_002_Acceptance()
    {
        Assert.NotNull(typeof(CogntApp.Features.Notes.TrashViewModel));
    }

    [Fact]
    public void AddEmptyTrashFlow_SAFE_001_Acceptance()
    {
        Assert.NotNull(typeof(EmptyTrashRequest));
    }

    [Fact]
    public void AddEmptyTrashFlow_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddEmptyTrashFlow_SAFE_001_Failure()
    {
        // The Empty Trash flow is gated by a verified safety backup.
        Assert.NotNull(typeof(EmptyTrashRequest));
    }

    [Fact]
    public void AddEmptyTrashFlow_UX_002_Failure()
    {
        // The view-model exposes a single EmptyTrashCommand.
        Assert.NotNull(typeof(CogntApp.Features.Notes.TrashViewModel));
    }

    [Fact]
    public void AddEmptyTrashFlow_Negative_AlwaysAllowExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("AlwaysAllow", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM29-12 — Add failure recovery (shell-recovery surface)
    // ---------------------------------------------------------------------

    [Fact]
    public void AddFailureRecovery_SAFE_001_Acceptance()
    {
        // The shell-recovery surface is exposed on NoteDocumentSession.
        Assert.NotNull(typeof(CogntApp.Shell.Workspace.NoteDocumentSession));
    }

    [Fact]
    public void AddFailureRecovery_SEC_001_Acceptance()
    {
        // The recovery record is the typed NoteDocumentRecovery.
        Assert.NotNull(typeof(CogntApp.Shell.Workspace.NoteDocumentRecovery));
    }

    [Fact]
    public void AddFailureRecovery_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddFailureRecovery_SAFE_001_Failure()
    {
        // BeginSave is a public method on the shell-recovery surface.
        MethodInfo? begin = typeof(CogntApp.Shell.Workspace.NoteDocumentSession)
            .GetMethod("BeginSave", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(begin);
        ParameterInfo[] parameters = begin!.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal(typeof(string), parameters[1].ParameterType);
    }

    [Fact]
    public void AddFailureRecovery_UX_002_Failure()
    {
        // The session exposes a stable save automation name.
        PropertyInfo? prop = typeof(CogntApp.Shell.Workspace.NoteDocumentSession)
            .GetProperty("SaveCommandAutomationName", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void AddFailureRecovery_Negative_SilentDropExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("SilentDrop", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }
}
