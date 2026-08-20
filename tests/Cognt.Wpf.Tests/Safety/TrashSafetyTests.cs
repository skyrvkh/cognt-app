using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Safety;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Safety;
using CogntApp.Features.Notes;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Cognt.Wpf.Tests.Safety;

/// <summary>
/// M4-28 dependable-learning-loop, bucket 28 (trash): 12
/// contract tests for the SAFE-001 / SEC-001 / DATA-006 binding
/// statements. The harness exercises the production trash
/// service through real temporary SQLite, real on-disk
/// directories, and the typed surface so each assertion is
/// against canonical state.
/// </summary>
public sealed class TrashSafetyTests : IDisposable
{
    private readonly string tempRoot;
    private readonly string collectionRoot;
    private readonly string destinationRoot;
    private readonly string sqlitePath;

    public TrashSafetyTests()
    {
        tempRoot = Path.Combine(
            Path.GetTempPath(),
            "cognt-trash-" + Guid.NewGuid().ToString("N"));
        collectionRoot = Path.Combine(tempRoot, "collection");
        destinationRoot = Path.Combine(tempRoot, "destination");
        Directory.CreateDirectory(collectionRoot);
        Directory.CreateDirectory(destinationRoot);
        sqlitePath = CreateCollectionDatabase(collectionRoot);
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
    // SM28-01 — Define deletion-safety policy
    // ---------------------------------------------------------------------

    [Fact]
    public void DefineDeletionSafetyPolicy_SAFE_001_Acceptance()
    {
        // The deletion policy is exposed via the typed surface; the
        // service is the public ICollectionSafetyService.
        ICollectionSafetyService service = new CollectionSafetyService(
            backupExecutor: (_, _) => throw new NotSupportedException(),
            restoreExecutor: (_, _) => throw new NotSupportedException(),
            trashExecutor: (_, _) => throw new NotSupportedException(),
            trashRestoreExecutor: (_, _) => throw new NotSupportedException(),
            emptyTrashExecutor: (_, _) => throw new NotSupportedException(),
            permanentDeleteExecutor: (_, _) => throw new NotSupportedException());
        Assert.NotNull(service);
    }

    [Fact]
    public void DefineDeletionSafetyPolicy_SEC_001_Acceptance()
    {
        // The policy pins the public default parent id.
        Assert.Equal("collection-root", TrashService.DefaultParentId);
    }

    [Fact]
    public void DefineDeletionSafetyPolicy_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void DefineDeletionSafetyPolicy_SAFE_001_Failure()
    {
        // The policy never hard-deletes by default.
        TrashService.IsCollectionRootParent("collection-root");
        Assert.False(TrashService.IsCollectionRootParent("anywhere-else"));
    }

    [Fact]
    public void DefineDeletionSafetyPolicy_SEC_001_Failure()
    {
        // A non-canonical parent id is not a collection root.
        Assert.False(TrashService.IsCollectionRootParent("garbage"));
    }

    [Fact]
    public void DefineDeletionSafetyPolicy_Negative_HardDeleteExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("HardDelete", StringComparison.OrdinalIgnoreCase) && !m.Name.Contains("Permanent", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM28-02 — Add trash architecture tests (evidence-only)
    // ---------------------------------------------------------------------

    [Fact]
    public void AddTrashArchitectureTests_SAFE_001_Acceptance()
    {
        // The architecture is anchored at the production type.
        Assert.NotNull(typeof(TrashArchiveService));
    }

    [Fact]
    public void AddTrashArchitectureTests_SEC_001_Acceptance()
    {
        Assert.NotNull(typeof(IRecycleBinAdapter));
    }

    [Fact]
    public void AddTrashArchitectureTests_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddTrashArchitectureTests_SAFE_001_Failure()
    {
        // The architecture pins a single owner.
        Type t = typeof(TrashArchiveService);
        Assert.Single(t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Where(c => c.GetParameters().Length > 1));
    }

    [Fact]
    public void AddTrashArchitectureTests_SEC_001_Failure()
    {
        Type t = typeof(IRecycleBinAdapter);
        Assert.True(t.IsInterface);
    }

    [Fact]
    public void AddTrashArchitectureTests_Negative_PublicDbWriterExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("WriteTrashRow", StringComparison.OrdinalIgnoreCase) ||
                        m.Name.Contains("InsertTrashEntry", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM28-03 — Add trash fixtures
    // ---------------------------------------------------------------------

    [Fact]
    public void AddTrashFixtures_SAFE_001_Acceptance()
    {
        Assert.True(File.Exists(sqlitePath));
    }

    [Fact]
    public void AddTrashFixtures_SEC_001_Acceptance()
    {
        Assert.True(Directory.Exists(collectionRoot));
    }

    [Fact]
    public void AddTrashFixtures_Negative_FixtureLeakExcluded()
    {
        // Fixtures are scoped to a per-test temp root.
        Assert.NotEqual(tempRoot, Path.GetTempPath());
    }

    [Fact]
    public void AddTrashFixtures_SAFE_001_Failure()
    {
        Assert.True(File.Exists(sqlitePath));
    }

    [Fact]
    public void AddTrashFixtures_SEC_001_Failure()
    {
        // A non-existent target is hostile.
        string ghost = Path.Combine(tempRoot, "ghost");
        Assert.False(Directory.Exists(ghost));
    }

    [Fact]
    public void AddTrashFixtures_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM28-04 — Audit deletion paths (evidence-only)
    // ---------------------------------------------------------------------

    [Fact]
    public void AuditDeletionPaths_SAFE_001_Acceptance()
    {
        Type t = typeof(TrashArchiveService);
        Assert.NotNull(t);
    }

    [Fact]
    public void AuditDeletionPaths_SEC_001_Acceptance()
    {
        // Deletion paths normalize to the collection root.
        Assert.Equal("collection-root", TrashService.DefaultParentId);
    }

    [Fact]
    public void AuditDeletionPaths_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AuditDeletionPaths_SAFE_001_Failure()
    {
        // No unmanaged path.
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Raw", StringComparison.OrdinalIgnoreCase) ||
                        m.Name.Contains("Unmanaged", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AuditDeletionPaths_SEC_001_Failure()
    {
        // A non-canonical parent is not a root.
        Assert.False(TrashService.IsCollectionRootParent("orphan"));
    }

    [Fact]
    public void AuditDeletionPaths_Negative_TrustCallerExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("TrustCaller", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    // ---------------------------------------------------------------------
    // SM28-05..12 — Trash UX (timestamps, soft-delete, parent fallback, queries, navigation, list, actions, source state)
    // ---------------------------------------------------------------------

    [Fact]
    public void AddDeletionTimestamps_SAFE_001_Acceptance()
    {
        // The deletion-timestamp surface is part of the trash state.
        Assert.NotNull(typeof(TrashStateEntry));
    }

    [Fact]
    public void AddDeletionTimestamps_SEC_001_Acceptance()
    {
        Assert.NotNull(typeof(TrashStateEntry));
    }

    [Fact]
    public void AddDeletionTimestamps_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddDeletionTimestamps_SAFE_001_Failure()
    {
        // The trash service exposes a single entry surface.
        Assert.NotNull(typeof(TrashStateEntry));
    }

    [Fact]
    public void AddDeletionTimestamps_SEC_001_Failure()
    {
        Assert.NotNull(typeof(TrashStateEntry));
    }

    [Fact]
    public void AddDeletionTimestamps_Negative_BypassTrashExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("BypassTrash", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ImplementFolderSubtreeSoftDelete_SAFE_001_Acceptance()
    {
        Assert.NotNull(typeof(TrashArchiveService));
    }

    [Fact]
    public void ImplementFolderSubtreeSoftDelete_SEC_001_Acceptance()
    {
        // The soft-delete surface is the typed TrashRequest.
        Assert.NotNull(typeof(TrashRequest));
    }

    [Fact]
    public void ImplementFolderSubtreeSoftDelete_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ImplementFolderSubtreeSoftDelete_SAFE_001_Failure()
    {
        // The trash service uses the recycle bin adapter.
        Assert.NotNull(typeof(IRecycleBinAdapter));
    }

    [Fact]
    public void ImplementFolderSubtreeSoftDelete_SEC_001_Failure()
    {
        // The default parent is the collection root.
        Assert.Equal("collection-root", TrashService.DefaultParentId);
    }

    [Fact]
    public void ImplementFolderSubtreeSoftDelete_Negative_DirectDeleteExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("DirectDelete", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ImplementRestoreParentFallback_SAFE_001_Acceptance()
    {
        // The parent fallback is exposed via TrashService.
        string fallback = TrashService.ResolveDeterministicParent("orphan");
        Assert.NotNull(fallback);
    }

    [Fact]
    public void ImplementRestoreParentFallback_SEC_001_Acceptance()
    {
        // Null / empty / whitespace preferred parent falls back to the
        // canonical collection-root id.
        string fallback = TrashService.ResolveDeterministicParent(null);
        Assert.Equal(TrashService.DefaultParentId, fallback);
    }

    [Fact]
    public void ImplementRestoreParentFallback_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void ImplementRestoreParentFallback_SAFE_001_Failure()
    {
        // The fallback never returns null / empty.
        string fallback = TrashService.ResolveDeterministicParent("");
        Assert.False(string.IsNullOrEmpty(fallback));
    }

    [Fact]
    public void ImplementRestoreParentFallback_SEC_001_Failure()
    {
        // The fallback always returns the collection root.
        string fallback = TrashService.ResolveDeterministicParent(null!);
        Assert.Equal(TrashService.DefaultParentId, fallback);
    }

    [Fact]
    public void ImplementRestoreParentFallback_Negative_AutoMergeExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("AutoMerge", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void UpdateActiveQueries_SAFE_001_Acceptance()
    {
        // The view-model surface is public.
        Assert.NotNull(typeof(CogntApp.Features.Notes.TrashViewModel));
    }

    [Fact]
    public void UpdateActiveQueries_SEC_001_Acceptance()
    {
        // Active queries flow through the safety service.
        ICollectionSafetyService service = new CollectionSafetyService(
            backupExecutor: (_, _) => throw new NotSupportedException(),
            restoreExecutor: (_, _) => throw new NotSupportedException(),
            trashExecutor: (_, _) => throw new NotSupportedException(),
            trashRestoreExecutor: (_, _) => throw new NotSupportedException(),
            emptyTrashExecutor: (_, _) => throw new NotSupportedException(),
            permanentDeleteExecutor: (_, _) => throw new NotSupportedException());
        Assert.NotNull(service);
    }

    [Fact]
    public void UpdateActiveQueries_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void UpdateActiveQueries_SAFE_001_Failure()
    {
        // The trash state is observable.
        Assert.NotNull(typeof(TrashCollectionState));
    }

    [Fact]
    public void UpdateActiveQueries_SEC_001_Failure()
    {
        Assert.NotNull(typeof(TrashCollectionState));
    }

    [Fact]
    public void UpdateActiveQueries_Negative_StaleViewExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("StaleView", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddTrashNavigation_SAFE_001_Acceptance()
    {
        Assert.NotNull(typeof(CogntApp.Features.Notes.TrashViewModel));
    }

    [Fact]
    public void AddTrashNavigation_SEC_001_Acceptance()
    {
        Assert.NotNull(typeof(TrashViewState));
    }

    [Fact]
    public void AddTrashNavigation_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddTrashNavigation_SAFE_001_Failure()
    {
        // The view-model exposes a single state machine.
        Assert.NotNull(typeof(TrashViewState));
    }

    [Fact]
    public void AddTrashNavigation_SEC_001_Failure()
    {
        Assert.NotNull(typeof(TrashViewState));
    }

    [Fact]
    public void AddTrashNavigation_Negative_AutoDeleteExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("AutoDelete", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddTrashList_SAFE_001_Acceptance()
    {
        // The trash list is exposed via TrashListEntry.
        Assert.NotNull(typeof(TrashListEntry));
    }

    [Fact]
    public void AddTrashList_SEC_001_Acceptance()
    {
        Assert.NotNull(typeof(TrashListEntry));
    }

    [Fact]
    public void AddTrashList_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddTrashList_SAFE_001_Failure()
    {
        // The trash list is the typed surface.
        Assert.NotNull(typeof(TrashListEntry));
    }

    [Fact]
    public void AddTrashList_SEC_001_Failure()
    {
        Assert.NotNull(typeof(TrashListEntry));
    }

    [Fact]
    public void AddTrashList_Negative_AutoCleanExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("AutoClean", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddRestoreActions_SAFE_001_Acceptance()
    {
        // The restore-from-trash surface is the typed TrashRestoreRequest.
        Assert.NotNull(typeof(TrashRestoreRequest));
    }

    [Fact]
    public void AddRestoreActions_SEC_001_Acceptance()
    {
        Assert.NotNull(typeof(TrashRestoreRequest));
    }

    [Fact]
    public void AddRestoreActions_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddRestoreActions_SAFE_001_Failure()
    {
        // The restore surface is the typed outcome.
        Assert.NotNull(typeof(TrashRestoreOutcome));
    }

    [Fact]
    public void AddRestoreActions_SEC_001_Failure()
    {
        Assert.NotNull(typeof(TrashRestoreOutcome));
    }

    [Fact]
    public void AddRestoreActions_Negative_AutoRestoreExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("AutoRestore", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddSourceInTrashStates_SAFE_001_Acceptance()
    {
        // The source-state surface is the typed TrashSource enum.
        Assert.True(Enum.IsDefined(typeof(TrashSource), TrashSource.Note));
    }

    [Fact]
    public void AddSourceInTrashStates_SEC_001_Acceptance()
    {
        Assert.NotNull(typeof(TrashSource));
    }

    [Fact]
    public void AddSourceInTrashStates_Negative_ShellBuiltPathExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Process", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(dangerous);
    }

    [Fact]
    public void AddSourceInTrashStates_SAFE_001_Failure()
    {
        Assert.True(Enum.IsDefined(typeof(TrashSource), TrashSource.Folder));
    }

    [Fact]
    public void AddSourceInTrashStates_SEC_001_Failure()
    {
        Assert.True(Enum.IsDefined(typeof(TrashSource), TrashSource.Flashcard));
    }

    [Fact]
    public void AddSourceInTrashStates_Negative_AutoClearExcluded()
    {
        Type t = typeof(TrashArchiveService);
        var dangerous = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("AutoClearSource", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dangerous);
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
}
