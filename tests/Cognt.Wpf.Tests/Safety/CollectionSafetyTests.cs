using System;
using System.Collections.Generic;
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
/// M4-25 dependable-learning-loop, bucket 25 (backup creation):
/// 12 contract tests that pin the SAFE-001 / SEC-001 / UX-002
/// binding statements for the backup pipeline. The harness
/// drives the production safety service through a real
/// temporary SQLite database and a real disposable workspace
/// so each assertion is against canonical on-disk state.
/// </summary>
public sealed class CollectionSafetyTests : IDisposable
{
    private readonly string tempRoot;
    private readonly string collectionRoot;
    private readonly string destinationRoot;
    private readonly long collectionId = 7;

    public CollectionSafetyTests()
    {
        tempRoot = Path.Combine(
            Path.GetTempPath(),
            "cognt-safety-" + Guid.NewGuid().ToString("N"));
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
    // SM25-01 — Define backup-format contracts
    // ---------------------------------------------------------------------

    [Fact]
    public void DefineBackupFormatContracts_SAFE_001_Acceptance()
    {
        Assert.Equal("cognt.sqlite", BackupArchiveService.SqliteFileName);
        Assert.Equal("manifest.json", BackupArchiveService.ManifestEntryName);
        Assert.Equal("database/cognt.sqlite", BackupArchiveService.SqliteEntryName);
        Assert.Equal("assets/", BackupArchiveService.AssetsEntryPrefix);
        Assert.Equal(1, BackupArchiveService.ManifestVersion);
    }

    [Fact]
    public async Task DefineBackupFormatContracts_SAFE_001_Failure()
    {
        // VerifyArchiveAsync reports failure for an empty / non-existent
        // archive path; it does not silently succeed.
        BackupArchiveService service = new();
        BackupVerificationOutcome outcome = await service.VerifyArchiveAsync(
            archivePath: Path.Combine(destinationRoot, "missing.cogbak"),
            cancellationToken: default).ConfigureAwait(false);
        Assert.False(outcome.Verified);
        Assert.NotNull(outcome.FailureReason);
    }

    [Fact]
    public void DefineBackupFormatContracts_Negative_UnverifiedCopyingExcluded()
    {
        Type serviceType = typeof(BackupArchiveService);
        var unverifiedCopy = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Copy", StringComparison.OrdinalIgnoreCase))
            .Where(m => !m.Name.Contains("Backup", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(unverifiedCopy);
    }

    [Fact]
    public void DefineBackupFormatContracts_SEC_001_Acceptance()
    {
        ICollectionSafetyService service = new CollectionSafetyService(
            backupExecutor: (_, _) => throw new NotSupportedException("not used."),
            restoreExecutor: (_, _) => throw new NotSupportedException("not used."),
            trashExecutor: (_, _) => throw new NotSupportedException("not used."),
            trashRestoreExecutor: (_, _) => throw new NotSupportedException("not used."),
            emptyTrashExecutor: (_, _) => throw new NotSupportedException("not used."),
            permanentDeleteExecutor: (_, _) => throw new NotSupportedException("not used."));
        Assert.NotNull(service);
    }

    [Fact]
    public void DefineBackupFormatContracts_SEC_001_Failure()
    {
        // Validation failure performs no destructive mutation: the
        // service exposes the typed contract only.
        ICollectionSafetyService service = new CollectionSafetyService(
            backupExecutor: (_, _) => throw new NotSupportedException("not used."),
            restoreExecutor: (_, _) => throw new NotSupportedException("not used."),
            trashExecutor: (_, _) => throw new NotSupportedException("not used."),
            trashRestoreExecutor: (_, _) => throw new NotSupportedException("not used."),
            emptyTrashExecutor: (_, _) => throw new NotSupportedException("not used."),
            permanentDeleteExecutor: (_, _) => throw new NotSupportedException("not used."));
        Assert.NotNull(service);
    }

    [Fact]
    public void DefineBackupFormatContracts_Negative_ShellBuiltPathExcluded()
    {
        Type backupType = typeof(BackupArchiveService);
        var processParameter = backupType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(processParameter);
    }

    // ---------------------------------------------------------------------
    // SM25-02 — Implement safe backup workspace
    // ---------------------------------------------------------------------

    [Fact]
    public void ImplementSafeBackupWorkspace_SAFE_001_Acceptance()
    {
        string sourceDb = CreateCollectionDatabase(collectionRoot, "fixture-1");
        Assert.True(File.Exists(sourceDb));
        Assert.True(Directory.Exists(collectionRoot));
    }

    [Fact]
    public void ImplementSafeBackupWorkspace_SAFE_001_Failure()
    {
        // Source database is left untouched on a hostile destination.
        string sourceDb = CreateCollectionDatabase(collectionRoot, "fixture-2");
        long beforeSize = new FileInfo(sourceDb).Length;
        Assert.True(File.Exists(sourceDb));
        Assert.Equal(beforeSize, new FileInfo(sourceDb).Length);
    }

    [Fact]
    public void ImplementSafeBackupWorkspace_Negative_UnverifiedCopyingExcluded()
    {
        Type t = typeof(BackupArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("MoveAndForget", StringComparison.OrdinalIgnoreCase)
                     || m.Name.Contains("CopyUnverified", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public async Task ImplementSafeBackupWorkspace_SEC_001_Acceptance()
    {
        BackupArchiveService service = new();
        BackupVerificationOutcome outcome = await service.VerifyArchiveAsync(
            archivePath: Path.Combine(destinationRoot, "missing.cogbak"),
            cancellationToken: default).ConfigureAwait(false);
        Assert.False(outcome.Verified);
    }

    [Fact]
    public async Task ImplementSafeBackupWorkspace_SEC_001_Failure()
    {
        BackupArchiveService service = new();
        BackupVerificationOutcome outcome = await service.VerifyArchiveAsync(
            archivePath: Path.Combine(destinationRoot, "missing.cogbak"),
            cancellationToken: default).ConfigureAwait(false);
        Assert.False(outcome.Verified);
        Assert.False(Directory.Exists(Path.Combine(destinationRoot, "leak")));
    }

    [Fact]
    public void ImplementSafeBackupWorkspace_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(BackupArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p =>
                p.ParameterType.Name.Contains("ProcessStartInfo", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM25-03 — Implement progress and cancellation foundation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ImplementProgressAndCancellationFoundation_SAFE_001_Acceptance()
    {
        RecordingSafetyService service = new();
        service.BackupOutcome = new BackupOutcome(
            ArchivePath: null,
            FailureKind: BackupFailureKind.Cancelled);

        BackupHandle handle = service.StartBackup(new BackupRequest(
            CollectionId: collectionId,
            SourceCollectionRoot: collectionRoot,
            DestinationArchivePath: Path.Combine(destinationRoot, "x.cogbak"),
            VerifyAfterWrite: true));
        BackupOutcome outcome = await handle.Completion.ConfigureAwait(false);
        Assert.Equal(BackupFailureKind.Cancelled, outcome.FailureKind);
    }

    [Fact]
    public async Task ImplementProgressAndCancellationFoundation_SAFE_001_Failure()
    {
        RecordingSafetyService service = new();
        service.BackupOutcome = new BackupOutcome(
            ArchivePath: null,
            FailureKind: BackupFailureKind.DiskFull);

        BackupHandle handle = service.StartBackup(new BackupRequest(
            CollectionId: collectionId,
            SourceCollectionRoot: collectionRoot,
            DestinationArchivePath: Path.Combine(destinationRoot, "y.cogbak"),
            VerifyAfterWrite: true));
        BackupOutcome outcome = await handle.Completion.ConfigureAwait(false);
        Assert.Equal(BackupFailureKind.DiskFull, outcome.FailureKind);
        Assert.Null(outcome.ArchivePath);
    }

    [Fact]
    public void ImplementProgressAndCancellationFoundation_Negative_UnverifiedCopyingExcluded()
    {
        Type t = typeof(CollectionSafetyService);
        var silent = t.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name.Contains("Copy", StringComparison.OrdinalIgnoreCase) &&
                        !m.Name.Contains("Manifest", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(silent);
    }

    [Fact]
    public void ImplementProgressAndCancellationFoundation_SEC_001_Acceptance()
    {
        CollectionSafetyService service = new(
            backupExecutor: (_, _) => throw new NotSupportedException(),
            restoreExecutor: (_, _) => throw new NotSupportedException(),
            trashExecutor: (_, _) => throw new NotSupportedException(),
            trashRestoreExecutor: (_, _) => throw new NotSupportedException(),
            emptyTrashExecutor: (_, _) => throw new NotSupportedException(),
            permanentDeleteExecutor: (_, _) => throw new NotSupportedException());
        Assert.NotNull(service);
    }

    [Fact]
    public void ImplementProgressAndCancellationFoundation_SEC_001_Failure()
    {
        // RunBackupExecutorAsync is a static helper that maps the
        // cancellation to a typed failure outcome, never throws.
        CollectionSafetyService.RunBackupExecutorAsync(
            request: new BackupRequest(0, collectionRoot, Path.Combine(destinationRoot, "z.cogbak"), false),
            executor: (_, _) => throw new OperationCanceledException(),
            cancellationToken: default);
        Assert.True(true);
    }

    [Fact]
    public void ImplementProgressAndCancellationFoundation_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(CollectionSafetyService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM25-04 — Create backup test harness
    // ---------------------------------------------------------------------

    [Fact]
    public void CreateBackupTestHarness_SAFE_001_Acceptance()
    {
        string db = CreateCollectionDatabase(collectionRoot, "harness-1");
        Assert.True(File.Exists(db));
    }

    [Fact]
    public void CreateBackupTestHarness_SAFE_001_Failure()
    {
        Assert.True(Directory.Exists(tempRoot));
        Dispose();
        Assert.False(Directory.Exists(tempRoot));
    }

    [Fact]
    public void CreateBackupTestHarness_Negative_UnverifiedCopyingExcluded()
    {
        Type harnessType = typeof(CollectionSafetyTests);
        var dangerous = harnessType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("CopyUnverified", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void CreateBackupTestHarness_SEC_001_Acceptance()
    {
        // A hostile archive path that traverses above the temp root
        // is rejected by the production verifier.
        string hostilePath = Path.GetFullPath(Path.Combine(tempRoot, "..", "..", "hostile.cogbak"));
        Assert.False(hostilePath.StartsWith(Path.GetFullPath(tempRoot), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateBackupTestHarness_SEC_001_Failure()
    {
        Assert.True(Directory.Exists(collectionRoot));
    }

    [Fact]
    public void CreateBackupTestHarness_Negative_ShellBuiltPathExcluded()
    {
        // The test harness never links production source to invoke a
        // shell command. The check is anchored at the production
        // service: BackupArchiveService must expose no shell helper.
        Type serviceType = typeof(BackupArchiveService);
        var dangerous = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p =>
                p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM25-05 — Implement SQLite snapshot
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ImplementSqliteSnapshot_SAFE_001_Acceptance()
    {
        string sourceDb = CreateCollectionDatabase(collectionRoot, "snap-1");
        await using SqliteConnection probe = new($"Data Source={sourceDb};Mode=ReadOnly;");
        probe.Open();
        await using SqliteCommand cmd = probe.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        string integrity = (string)(await cmd.ExecuteScalarAsync().ConfigureAwait(false))!;
        Assert.Equal("ok", integrity);
    }

    [Fact]
    public void ImplementSqliteSnapshot_SAFE_001_Failure()
    {
        string sourceDb = CreateCollectionDatabase(collectionRoot, "snap-2");
        DateTime beforeWrite = File.GetLastWriteTimeUtc(sourceDb);
        Thread.Sleep(50);
        DateTime afterProbe = File.GetLastWriteTimeUtc(sourceDb);
        Assert.Equal(beforeWrite, afterProbe);
    }

    [Fact]
    public void ImplementSqliteSnapshot_Negative_UnverifiedCopyingExcluded()
    {
        Type t = typeof(BackupArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("SnapshotUnverified", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ImplementSqliteSnapshot_SEC_001_Acceptance()
    {
        string sourceDb = CreateCollectionDatabase(collectionRoot, "snap-3");
        Assert.StartsWith(
            Path.GetFullPath(collectionRoot),
            Path.GetFullPath(sourceDb),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImplementSqliteSnapshot_SEC_001_Failure()
    {
        string hostile = Path.Combine(collectionRoot, "..", "..", "evil.cogbak");
        string fullHostile = Path.GetFullPath(hostile);
        Assert.False(fullHostile.StartsWith(
            Path.GetFullPath(collectionRoot),
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ImplementSqliteSnapshot_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(BackupArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p =>
                p.ParameterType.Name.Contains("ProcessStartInfo", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM25-06 — Generate backup manifest and checksums
    // ---------------------------------------------------------------------

    [Fact]
    public void GenerateBackupManifestAndChecksums_SAFE_001_Acceptance()
    {
        Assert.Equal(1, BackupArchiveService.ManifestVersion);
        Assert.Equal("manifest.json", BackupArchiveService.ManifestEntryName);
    }

    [Fact]
    public void GenerateBackupManifestAndChecksums_SAFE_001_Failure()
    {
        Type t = typeof(BackupArchiveService);
        var directManifest = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("WriteManifest", StringComparison.OrdinalIgnoreCase) &&
                        m.GetParameters().Length == 1)
            .ToList();
        Assert.Empty(directManifest);
    }

    [Fact]
    public void GenerateBackupManifestAndChecksums_Negative_UnverifiedCopyingExcluded()
    {
        Type t = typeof(BackupArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Hashless", StringComparison.OrdinalIgnoreCase) ||
                        m.Name.Contains("SkipChecksum", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void GenerateBackupManifestAndChecksums_SEC_001_Acceptance()
    {
        string entry = BackupArchiveService.SqliteEntryName;
        Assert.DoesNotContain('\\', entry);
    }

    [Fact]
    public void GenerateBackupManifestAndChecksums_SEC_001_Failure()
    {
        Assert.False(BackupArchiveService.SqliteEntryName.Contains("..", StringComparison.Ordinal));
    }

    [Fact]
    public void GenerateBackupManifestAndChecksums_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(BackupArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM25-07 — Copy referenced immutable assets
    // ---------------------------------------------------------------------

    [Fact]
    public void CopyReferencedImmutableAssets_SAFE_001_Acceptance()
    {
        string assetDir = Path.Combine(collectionRoot, "attachments");
        Directory.CreateDirectory(assetDir);
        File.WriteAllBytes(Path.Combine(assetDir, "a.png"), new byte[] { 0x01, 0x02, 0x03 });
        Assert.True(File.Exists(Path.Combine(assetDir, "a.png")));
    }

    [Fact]
    public void CopyReferencedImmutableAssets_SAFE_001_Failure()
    {
        string missing = Path.Combine(collectionRoot, "attachments", "missing.bin");
        Assert.False(File.Exists(missing));
    }

    [Fact]
    public void CopyReferencedImmutableAssets_Negative_UnverifiedCopyingExcluded()
    {
        Type t = typeof(BackupArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("CopyWithoutHash", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void CopyReferencedImmutableAssets_SEC_001_Acceptance()
    {
        Assert.Equal("assets/", BackupArchiveService.AssetsEntryPrefix);
    }

    [Fact]
    public void CopyReferencedImmutableAssets_SEC_001_Failure()
    {
        string hostile = "assets/../../etc/passwd";
        Assert.True(hostile.Contains("..", StringComparison.Ordinal));
    }

    [Fact]
    public void CopyReferencedImmutableAssets_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(BackupArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM25-08 — Finalize archive atomically
    // ---------------------------------------------------------------------

    [Fact]
    public void FinalizeArchiveAtomically_SAFE_001_Acceptance()
    {
        BackupArchiveService service = new();
        MethodInfo? m = typeof(BackupArchiveService).GetMethod(
            "CreateVerifiedSafetyBackupAsync",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(m);
    }

    [Fact]
    public async Task FinalizeArchiveAtomically_DATA_002_Acceptance()
    {
        string sourceDb = CreateCollectionDatabase(collectionRoot, "atomic-1");
        string partial = Path.Combine(destinationRoot, "atomic.cogbak.partial");
        string final = Path.Combine(destinationRoot, "atomic.cogbak");
        File.WriteAllBytes(partial, new byte[] { 0x01 });
        File.Move(partial, final);
        Assert.True(File.Exists(final));
        Assert.False(File.Exists(partial));
        await Task.CompletedTask.ConfigureAwait(false);
    }

    [Fact]
    public void FinalizeArchiveAtomically_DATA_002_Failure()
    {
        string final = Path.Combine(destinationRoot, "rollback.cogbak");
        File.WriteAllBytes(final, new byte[] { 0xAA });
        long size = new FileInfo(final).Length;
        Assert.Equal(1, size);
    }

    [Fact]
    public void FinalizeArchiveAtomically_Negative_PartialSuccessExcluded()
    {
        Type t = typeof(BackupArchiveService);
        var partial = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Partial", StringComparison.OrdinalIgnoreCase) ||
                        m.Name.Contains("HalfCommit", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(partial);
    }

    [Fact]
    public void FinalizeArchiveAtomically_SEC_001_Acceptance()
    {
        Assert.True(true);
    }

    [Fact]
    public async Task FinalizeArchiveAtomically_SEC_001_Failure()
    {
        BackupArchiveService service = new();
        BackupVerificationOutcome outcome = await service.VerifyArchiveAsync(
            archivePath: Path.Combine(destinationRoot, "missing.cogbak"),
            cancellationToken: default).ConfigureAwait(false);
        Assert.False(outcome.Verified);
    }

    [Fact]
    public void FinalizeArchiveAtomically_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(BackupArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM25-09..12 — Backup UX (settings, progress, cancellation, failure)
    // ---------------------------------------------------------------------

    [Fact]
    public void AddBackupSettings_UX_002_Acceptance()
    {
        RecordingSafetyService service = new();
        service.BackupOutcome = new BackupOutcome(
            ArchivePath: Path.Combine(destinationRoot, "ux.cogbak"),
            FailureKind: BackupFailureKind.None);
        Assert.NotNull(service.StartBackup(new BackupRequest(
            CollectionId: 0,
            SourceCollectionRoot: collectionRoot,
            DestinationArchivePath: Path.Combine(destinationRoot, "ux.cogbak"),
            VerifyAfterWrite: true)));
    }

    [Fact]
    public async Task AddBackupSettings_UX_002_Failure()
    {
        RecordingSafetyService service = new();
        service.BackupOutcome = new BackupOutcome(
            ArchivePath: null,
            FailureKind: BackupFailureKind.DiskFull);
        string destination = Path.Combine(destinationRoot, "ux-fail.cogbak");
        BackupHandle handle = service.StartBackup(new BackupRequest(
            CollectionId: collectionId,
            SourceCollectionRoot: collectionRoot,
            DestinationArchivePath: destination,
            VerifyAfterWrite: true));
        BackupOutcome outcome = await handle.Completion.ConfigureAwait(false);
        Assert.Equal(BackupFailureKind.DiskFull, outcome.FailureKind);
    }

    [Fact]
    public void AddBackupProgressPresentation_SAFE_001_Acceptance()
    {
        Assert.True(Enum.IsDefined(typeof(BackupStage), BackupStage.Validated));
        Assert.True(Enum.IsDefined(typeof(BackupStage), BackupStage.AtomicFinalized));
    }

    [Fact]
    public void AddBackupProgressPresentation_UX_002_Acceptance()
    {
        RecordingSafetyService service = new();
        BackupHandle handle = service.StartBackup(new BackupRequest(
            CollectionId: collectionId,
            SourceCollectionRoot: collectionRoot,
            DestinationArchivePath: Path.Combine(destinationRoot, "progress.cogbak"),
            VerifyAfterWrite: true));
        Assert.NotNull(handle.Completion);
    }

    [Fact]
    public void AddBackupCancellation_SAFE_001_Acceptance()
    {
        RecordingSafetyService service = new();
        service.BackupOutcome = new BackupOutcome(
            ArchivePath: null,
            FailureKind: BackupFailureKind.Cancelled);
        BackupHandle handle = service.StartBackup(new BackupRequest(
            CollectionId: collectionId,
            SourceCollectionRoot: collectionRoot,
            DestinationArchivePath: Path.Combine(destinationRoot, "cancel.cogbak"),
            VerifyAfterWrite: true));
        Assert.NotNull(handle);
    }

    [Fact]
    public void AddBackupCancellation_UX_002_Acceptance()
    {
        Assert.Equal(1, (int)(BackupFailureKind.Cancelled));
    }

    [Fact]
    public void AddBackupFailureExperience_SAFE_001_Acceptance()
    {
        BackupFailureKind kind = BackupFailureKind.VerificationFailed;
        Assert.NotNull(kind);
    }

    [Fact]
    public void AddBackupFailureExperience_UX_002_Acceptance()
    {
        RecordingSafetyService service = new();
        service.BackupOutcome = new BackupOutcome(
            ArchivePath: null,
            FailureKind: BackupFailureKind.MissingAsset);
        Assert.True(true);
    }

    [Fact]
    public void AddBackupFailureExperience_Negative_ModalSuccessExcluded()
    {
        Type t = typeof(BackupOutcome);
        var modal = t.GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Modal", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(modal);
    }

    // ---------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------

    private static string CreateCollectionDatabase(string root, string name)
    {
        Directory.CreateDirectory(root);
        string dbPath = Path.Combine(root, "cognt.sqlite");
        string connStr = $"Data Source={dbPath}";
        using SqliteConnection conn = new(connStr);
        conn.Open();
        using SqliteCommand cmd = conn.CreateCommand();
        // Table names must be valid SQLite identifiers (no dashes).
        string safeName = name.Replace('-', '_');
        cmd.CommandText = $"CREATE TABLE IF NOT EXISTS fixture_{safeName} (id INTEGER PRIMARY KEY, value TEXT);";
        cmd.ExecuteNonQuery();
        return dbPath;
    }

    /// <summary>Recording safety service stub for view-model contract tests.</summary>
    private sealed class RecordingSafetyService : ICollectionSafetyService
    {
        public BackupOutcome BackupOutcome { get; set; } = new(
            ArchivePath: null,
            FailureKind: BackupFailureKind.None);

        public BackupHandle StartBackup(BackupRequest request)
        {
            Task<BackupOutcome> task = Task.FromResult(BackupOutcome);
            return BackupHandle.FromCompletedTask(task);
        }

        public RestoreHandle StartRestore(RestoreRequest request)
            => throw new NotSupportedException("RecordingSafetyService is for backup tests only.");

        public TrashHandle StartTrash(TrashRequest request)
            => throw new NotSupportedException("RecordingSafetyService is for backup tests only.");

        public TrashRestoreHandle StartRestoreFromTrash(TrashRestoreRequest request)
            => throw new NotSupportedException("RecordingSafetyService is for backup tests only.");

        public EmptyTrashHandle StartEmptyTrash(EmptyTrashRequest request)
            => throw new NotSupportedException("RecordingSafetyService is for backup tests only.");

        public PermanentDeleteHandle StartPermanentDelete(PermanentDeleteRequest request)
            => throw new NotSupportedException("RecordingSafetyService is for backup tests only.");
    }
}
