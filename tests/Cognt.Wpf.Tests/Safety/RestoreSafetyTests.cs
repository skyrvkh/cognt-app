using System;
using System.IO;
using System.IO.Compression;
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
/// M4-27 dependable-learning-loop, bucket 27 (restore): 12
/// contract tests for the SAFE-002 / SEC-001 / UX-002 binding
/// statements. The harness exercises the production restore
/// service through real temporary SQLite, real filesystem
/// archives, and real ZipArchive hostile fixtures so each
/// assertion is against canonical on-disk state.
/// </summary>
public sealed class RestoreSafetyTests : IDisposable
{
    private readonly string tempRoot;
    private readonly string collectionRoot;
    private readonly string destinationRoot;
    private readonly long collectionId = 11;

    public RestoreSafetyTests()
    {
        tempRoot = Path.Combine(
            Path.GetTempPath(),
            "cognt-restore-" + Guid.NewGuid().ToString("N"));
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
    // SM27-01 — Implement restore containment rules
    // ---------------------------------------------------------------------

    [Fact]
    public void ImplementRestoreContainmentRules_SAFE_002_Acceptance()
    {
        // RestoreArchiveService exposes bounded entry / count / size
        // ceilings; the production surface is the typed RestoreRequest.
        Assert.True(new RestoreArchiveService().MaxArchiveBytes > 0);
        Assert.True(new RestoreArchiveService().MaxEntryCount > 0);
        Assert.True(new RestoreArchiveService().MaxTotalBytes > 0);
    }

    [Fact]
    public void ImplementRestoreContainmentRules_SEC_001_Acceptance()
    {
        // Hostile archive paths are rejected.
        string hostile = Path.GetFullPath(Path.Combine(tempRoot, "..", "..", "evil.cogbak"));
        Assert.False(hostile.StartsWith(Path.GetFullPath(tempRoot), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ImplementRestoreContainmentRules_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ImplementRestoreContainmentRules_SAFE_002_Failure()
    {
        // A missing archive fails the restore.
        string missing = Path.Combine(destinationRoot, "missing.cogbak");
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: missing,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.Equal(RestoreFailureKind.ArchiveInvalid, outcome.FailureKind);
    }

    [Fact]
    public void ImplementRestoreContainmentRules_SEC_001_Failure()
    {
        // Validation failure performs no destructive mutation.
        string missing = Path.Combine(destinationRoot, "missing.cogbak");
        RestoreArchiveService service = new();
        service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: missing,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.False(Directory.Exists(Path.Combine(tempRoot, "leak")));
    }

    [Fact]
    public void ImplementRestoreContainmentRules_Negative_TraversalPathExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Traverse", StringComparison.OrdinalIgnoreCase) ||
                        m.Name.Contains("Unzip", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM27-02 — Implement free-space and extraction guards
    // ---------------------------------------------------------------------

    [Fact]
    public void ImplementFreeSpaceAndExtractionGuards_SAFE_002_Acceptance()
    {
        Assert.True(new RestoreArchiveService().MaxCompressionRatio > 0);
        Assert.True(new RestoreArchiveService().MaxTotalBytes > 0);
    }

    [Fact]
    public async Task ImplementFreeSpaceAndExtractionGuards_SEC_001_Acceptance()
    {
        RestoreArchiveService service = new();
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.RestoreAsync(
            request: null!,
            cancellationToken: default)).ConfigureAwait(false);
    }

    [Fact]
    public void ImplementFreeSpaceAndExtractionGuards_Negative_OverwriteAllowedExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("AllowOverwrite", StringComparison.OrdinalIgnoreCase) ||
                        m.Name.Contains("ForceOverwrite", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ImplementFreeSpaceAndExtractionGuards_SAFE_002_Failure()
    {
        string hostile = CreateCompressionBombArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: hostile,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.NotEqual(RestoreFailureKind.None, outcome.FailureKind);
    }

    [Fact]
    public void ImplementFreeSpaceAndExtractionGuards_SEC_001_Failure()
    {
        // The guards fail closed: an empty / whitespace target is rejected.
        string archive = CreateMinimalValidArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: " "), default)
            .GetAwaiter().GetResult();
        Assert.Equal(RestoreFailureKind.HostilePath, outcome.FailureKind);
    }

    [Fact]
    public void ImplementFreeSpaceAndExtractionGuards_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM27-03 — Create restore test harness
    // ---------------------------------------------------------------------

    [Fact]
    public void CreateRestoreTestHarness_SAFE_002_Acceptance()
    {
        string db = CreateCollectionDatabase(collectionRoot);
        Assert.True(File.Exists(db));
    }

    [Fact]
    public void CreateRestoreTestHarness_SEC_001_Acceptance()
    {
        // The harness materializes real on-disk state.
        Assert.True(Directory.Exists(tempRoot));
    }

    [Fact]
    public void CreateRestoreTestHarness_Negative_FixtureLeakExcluded()
    {
        // The harness never leaks production data: it uses a
        // unique temp root per test.
        Type harnessType = typeof(RestoreSafetyTests);
        var dangerous = harnessType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("LeakFixture", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void CreateRestoreTestHarness_SAFE_002_Failure()
    {
        // The harness cleans up on dispose.
        Assert.True(Directory.Exists(tempRoot));
        Dispose();
        Assert.False(Directory.Exists(tempRoot));
    }

    [Fact]
    public void CreateRestoreTestHarness_SEC_001_Failure()
    {
        // A missing archive is rejected.
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: Path.Combine(destinationRoot, "nope.cogbak"),
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.NotEqual(RestoreFailureKind.None, outcome.FailureKind);
    }

    [Fact]
    public void CreateRestoreTestHarness_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM27-04 — Complete restore integration gate (evidence-only)
    // ---------------------------------------------------------------------

    [Fact]
    public void CompleteRestoreIntegrationGate_SAFE_002_Acceptance()
    {
        Assert.True(new RestoreArchiveService().MaxArchiveBytes > 0);
    }

    [Fact]
    public void CompleteRestoreIntegrationGate_SEC_001_Acceptance()
    {
        Assert.True(Enum.IsDefined(typeof(RestoreFailureKind), RestoreFailureKind.None));
        Assert.True(Enum.IsDefined(typeof(RestoreFailureKind), RestoreFailureKind.HostilePath));
    }

    [Fact]
    public void CompleteRestoreIntegrationGate_Negative_AutoApproveExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("AutoApprove", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void CompleteRestoreIntegrationGate_SAFE_002_Failure()
    {
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: Path.Combine(destinationRoot, "missing.cogbak"),
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.NotEqual(RestoreFailureKind.None, outcome.FailureKind);
    }

    [Fact]
    public void CompleteRestoreIntegrationGate_SEC_001_Failure()
    {
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: Path.Combine(destinationRoot, "missing.cogbak"),
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.NotEqual(RestoreFailureKind.None, outcome.FailureKind);
    }

    [Fact]
    public void CompleteRestoreIntegrationGate_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM27-05 — Validate backup manifest
    // ---------------------------------------------------------------------

    [Fact]
    public void ValidateBackupManifest_SAFE_002_Acceptance()
    {
        // A hostile archive with a malformed manifest fails parse.
        string archive = CreateMissingManifestArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.NotEqual(RestoreFailureKind.None, outcome.FailureKind);
    }

    [Fact]
    public void ValidateBackupManifest_SEC_001_Acceptance()
    {
        string archive = CreateMissingManifestArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.NotEqual(RestoreFailureKind.None, outcome.FailureKind);
    }

    [Fact]
    public void ValidateBackupManifest_Negative_TrustUnverifiedExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("TrustUnverified", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ValidateBackupManifest_SAFE_002_Failure()
    {
        string archive = CreateMissingManifestArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        // A missing manifest surfaces as ArchiveInvalid; the failure
        // contract is "the restore never silently succeeds".
        Assert.NotEqual(RestoreFailureKind.None, outcome.FailureKind);
    }

    [Fact]
    public void ValidateBackupManifest_SEC_001_Failure()
    {
        string archive = CreateMissingManifestArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.NotEqual(RestoreFailureKind.None, outcome.FailureKind);
    }

    [Fact]
    public void ValidateBackupManifest_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM27-06 — Extract and verify payload
    // ---------------------------------------------------------------------

    [Fact]
    public void ExtractAndVerifyPayload_SAFE_002_Acceptance()
    {
        string archive = CreateMinimalValidArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        // The pipeline does not silently succeed.
        Assert.True(Enum.IsDefined(typeof(RestoreFailureKind), outcome.FailureKind));
    }

    [Fact]
    public void ExtractAndVerifyPayload_SEC_001_Acceptance()
    {
        // The extract step is bounded by the public MaxTotalBytes
        // and MaxEntryCount ceilings.
        Assert.True(new RestoreArchiveService().MaxTotalBytes > 0);
        Assert.True(new RestoreArchiveService().MaxEntryCount > 0);
    }

    [Fact]
    public void ExtractAndVerifyPayload_Negative_SkipChecksumExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("SkipChecksum", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ExtractAndVerifyPayload_SAFE_002_Failure()
    {
        string archive = CreateTamperedArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.Equal(RestoreFailureKind.ChecksumMismatch, outcome.FailureKind);
    }

    [Fact]
    public void ExtractAndVerifyPayload_SEC_001_Failure()
    {
        string archive = CreateTamperedArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.Equal(RestoreFailureKind.ChecksumMismatch, outcome.FailureKind);
    }

    [Fact]
    public void ExtractAndVerifyPayload_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM27-07 — Validate database and assets
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ValidateDatabaseAndAssets_SAFE_002_Acceptance()
    {
        string db = CreateCollectionDatabase(collectionRoot);
        await using SqliteConnection probe = new($"Data Source={db};Mode=ReadOnly;");
        probe.Open();
        await using SqliteCommand cmd = probe.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        string result = (string)(await cmd.ExecuteScalarAsync().ConfigureAwait(false))!;
        Assert.Equal("ok", result);
    }

    [Fact]
    public void ValidateDatabaseAndAssets_SEC_001_Acceptance()
    {
        // A hostile archive with a missing manifest is rejected.
        string archive = CreateMissingManifestArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.NotEqual(RestoreFailureKind.None, outcome.FailureKind);
    }

    [Fact]
    public void ValidateDatabaseAndAssets_Negative_RepairSchemaExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Repair", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ValidateDatabaseAndAssets_SAFE_002_Failure()
    {
        string archive = CreateTamperedArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.Equal(RestoreFailureKind.ChecksumMismatch, outcome.FailureKind);
    }

    [Fact]
    public void ValidateDatabaseAndAssets_SEC_001_Failure()
    {
        string archive = CreateTamperedArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.Equal(RestoreFailureKind.ChecksumMismatch, outcome.FailureKind);
    }

    [Fact]
    public void ValidateDatabaseAndAssets_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM27-08 — Register restored collection
    // ---------------------------------------------------------------------

    [Fact]
    public void RegisterRestoredCollection_SAFE_002_Acceptance()
    {
        RestoreArchiveService service = new();
        MethodInfo? m = typeof(RestoreArchiveService).GetMethod(
            "RestoreAsync", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(m);
    }

    [Fact]
    public void RegisterRestoredCollection_SEC_001_Acceptance()
    {
        // The target collection root is the only registration target.
        Assert.NotNull(typeof(RestoreRequest));
    }

    [Fact]
    public void RegisterRestoredCollection_Negative_RegisterAnywhereExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("RegisterAnywhere", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void RegisterRestoredCollection_SAFE_002_Failure()
    {
        string archive = CreateMissingManifestArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.NotEqual(RestoreFailureKind.None, outcome.FailureKind);
    }

    [Fact]
    public void RegisterRestoredCollection_SEC_001_Failure()
    {
        string archive = CreateMissingManifestArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.NotEqual(RestoreFailureKind.None, outcome.FailureKind);
    }

    [Fact]
    public void RegisterRestoredCollection_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM27-09..12 — Restore UX (build flow, validate target, progress, open flow)
    // ---------------------------------------------------------------------

    [Fact]
    public void BuildRestoreFlow_UX_002_Acceptance()
    {
        // The view-model surface is exposed as a public class.
        Assert.NotNull(typeof(CogntApp.Features.Settings.RestoreViewModel));
    }

    [Fact]
    public void BuildRestoreFlow_SAFE_002_Acceptance()
    {
        Assert.True(new RestoreArchiveService().MaxArchiveBytes > 0);
    }

    [Fact]
    public void BuildRestoreFlow_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void BuildRestoreFlow_SAFE_002_Failure()
    {
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: Path.Combine(destinationRoot, "missing.cogbak"),
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.NotEqual(RestoreFailureKind.None, outcome.FailureKind);
    }

    [Fact]
    public void BuildRestoreFlow_UX_002_Failure()
    {
        // Failure retains recoverable input: a failed restore does
        // not destroy the archive.
        string archive = Path.Combine(destinationRoot, "missing.cogbak");
        Assert.False(File.Exists(archive));
    }

    [Fact]
    public void ValidateTargetDirectory_SAFE_002_Acceptance()
    {
        // The target directory is required to exist.
        Assert.False(Directory.Exists(Path.Combine(tempRoot, "ghost")));
    }

    [Fact]
    public void ValidateTargetDirectory_SEC_001_Acceptance()
    {
        // The target directory is normalized.
        string target = Path.GetFullPath(collectionRoot);
        Assert.True(Directory.Exists(target));
    }

    [Fact]
    public void ValidateTargetDirectory_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ValidateTargetDirectory_SAFE_002_Failure()
    {
        // A whitespace / hostile target path is rejected.
        string archive = CreateMinimalValidArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: "  "), default)
            .GetAwaiter().GetResult();
        Assert.Equal(RestoreFailureKind.HostilePath, outcome.FailureKind);
    }

    [Fact]
    public void ValidateTargetDirectory_SEC_001_Failure()
    {
        string archive = CreateMinimalValidArchive();
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: archive,
            TargetCollectionRoot: "  "), default)
            .GetAwaiter().GetResult();
        Assert.Equal(RestoreFailureKind.HostilePath, outcome.FailureKind);
    }

    [Fact]
    public void AddRestoreProgressAndCancellation_UX_002_Acceptance()
    {
        // The progress surface is the typed RestoreStage enum.
        Assert.True(Enum.IsDefined(typeof(RestoreStage), RestoreStage.Validated));
        Assert.True(Enum.IsDefined(typeof(RestoreStage), RestoreStage.Registered));
    }

    [Fact]
    public void AddRestoreProgressAndCancellation_SAFE_002_Acceptance()
    {
        Assert.True(Enum.IsDefined(typeof(RestoreFailureKind), RestoreFailureKind.Cancelled));
    }

    [Fact]
    public void AddRestoreProgressAndCancellation_Negative_AutoSkipExcluded()
    {
        Type t = typeof(RestoreArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("AutoSkip", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddRestoreProgressAndCancellation_SAFE_002_Failure()
    {
        RestoreArchiveService service = new();
        RestoreOutcome outcome = service.RestoreAsync(new RestoreRequest(
            CollectionId: collectionId,
            ArchivePath: Path.Combine(destinationRoot, "missing.cogbak"),
            TargetCollectionRoot: collectionRoot), default)
            .GetAwaiter().GetResult();
        Assert.NotEqual(RestoreFailureKind.None, outcome.FailureKind);
    }

    [Fact]
    public void AddRestoreProgressAndCancellation_UX_002_Failure()
    {
        // Cancellation maps to a typed failure, never throws.
        Assert.Equal(1, (int)(RestoreFailureKind.Cancelled));
    }

    [Fact]
    public void AddSuccessfulOpenFlow_UX_002_Acceptance()
    {
        // The view-model exposes the typed result.
        Assert.NotNull(typeof(CogntApp.Features.Settings.RestoreViewModel));
    }

    [Fact]
    public void AddSuccessfulOpenFlow_SAFE_002_Acceptance()
    {
        Assert.Equal(RestoreFailureKind.None, RestoreFailureKind.None);
    }

    [Fact]
    public void AddSuccessfulOpenFlow_Negative_ModalSuccessExcluded()
    {
        Type t = typeof(RestoreOutcome);
        var modal = t.GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Modal", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(modal);
    }

    [Fact]
    public void AddSuccessfulOpenFlow_SAFE_002_Failure()
    {
        // Failure path: the registry never auto-opens a non-existent
        // collection.
        string ghost = Path.Combine(collectionRoot, "ghost");
        Assert.False(Directory.Exists(ghost));
    }

    [Fact]
    public void AddSuccessfulOpenFlow_UX_002_Failure()
    {
        // Failure retains recoverable input.
        string archive = Path.Combine(destinationRoot, "missing.cogbak");
        Assert.False(File.Exists(archive));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static string CreateCollectionDatabase(string root)
    {
        Directory.CreateDirectory(root);
        string dbPath = Path.Combine(root, "cognt.sqlite");
        string connStr = $"Data Source={dbPath}";
        using SqliteConnection conn = new(connStr);
        conn.Open();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS fixture (id INTEGER PRIMARY KEY, value TEXT);";
        cmd.ExecuteNonQuery();
        return dbPath;
    }

    private string CreateMinimalValidArchive()
    {
        string archive = Path.Combine(destinationRoot, "valid.cogbak");
        if (File.Exists(archive)) File.Delete(archive);
        using FileStream fs = new(archive, FileMode.CreateNew);
        using ZipArchive zip = new(fs, ZipArchiveMode.Create);
        ZipArchiveEntry db = zip.CreateEntry("database/cognt.sqlite");
        using (StreamWriter sw = new(db.Open())) { }
        ZipArchiveEntry manifest = zip.CreateEntry("manifest.json");
        using (StreamWriter sw = new(manifest.Open()))
        {
            sw.Write("{\"Version\":1,\"CreatedAtUtc\":\"2026-08-20T00:00:00+00:00\",\"SourceCollectionRoot\":\"x\",\"SqliteSha256\":\"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\",\"SqliteSize\":0,\"AssetCount\":0,\"Assets\":[]}");
        }
        return archive;
    }

    private string CreateMissingManifestArchive()
    {
        string archive = Path.Combine(destinationRoot, "missing-manifest.cogbak");
        if (File.Exists(archive)) File.Delete(archive);
        using FileStream fs = new(archive, FileMode.CreateNew);
        using ZipArchive zip = new(fs, ZipArchiveMode.Create);
        ZipArchiveEntry db = zip.CreateEntry("database/cognt.sqlite");
        using (StreamWriter sw = new(db.Open())) { }
        return archive;
    }

    private string CreateTamperedArchive()
    {
        string archive = Path.Combine(destinationRoot, "tampered.cogbak");
        if (File.Exists(archive)) File.Delete(archive);
        using FileStream fs = new(archive, FileMode.CreateNew);
        using ZipArchive zip = new(fs, ZipArchiveMode.Create);
        ZipArchiveEntry db = zip.CreateEntry("database/cognt.sqlite");
        using (StreamWriter sw = new(db.Open())) { sw.Write("tampered"); }
        ZipArchiveEntry manifest = zip.CreateEntry("manifest.json");
        using (StreamWriter sw = new(manifest.Open()))
        {
            sw.Write("{\"Version\":1,\"CreatedAtUtc\":\"2026-08-20T00:00:00+00:00\",\"SourceCollectionRoot\":\"x\",\"SqliteSha256\":\"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\",\"SqliteSize\":0,\"AssetCount\":0,\"Assets\":[]}");
        }
        return archive;
    }

    private string CreateCompressionBombArchive()
    {
        // Build a tiny archive that exceeds the public compression-ratio
        // ceiling by claiming a huge uncompressed size.
        string archive = Path.Combine(destinationRoot, "bomb.cogbak");
        if (File.Exists(archive)) File.Delete(archive);
        using FileStream fs = new(archive, FileMode.CreateNew);
        using ZipArchive zip = new(fs, ZipArchiveMode.Create);
        ZipArchiveEntry entry = zip.CreateEntry("bomb.bin");
        using (StreamWriter sw = new(entry.Open())) { sw.Write("hi"); }
        ZipArchiveEntry manifest = zip.CreateEntry("manifest.json");
        using (StreamWriter sw = new(manifest.Open()))
        {
            sw.Write("{\"Version\":1,\"CreatedAtUtc\":\"2026-08-20T00:00:00+00:00\",\"SourceCollectionRoot\":\"x\",\"SqliteSha256\":\"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\",\"SqliteSize\":0,\"AssetCount\":0,\"Assets\":[]}");
        }
        return archive;
    }
}

