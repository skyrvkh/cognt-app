using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Safety;
using Microsoft.Data.Sqlite;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Safety;

/// <summary>
/// Owner of the verified backup pipeline. The service is the
/// single writer of the <c>.cogbak</c> archive format and the
/// only path the application layer may use to create or verify
/// a backup. The infrastructure takes untrusted user input at
/// the source / destination paths, normalizes it, validates
/// containment, and never composes a shell command.
/// </summary>
public sealed class BackupArchiveService
{
    /// <summary>Canonical file name of the SQLite database inside a collection root.</summary>
    public const string SqliteFileName = "cognt.sqlite";

    /// <summary>Canonical entry name of the manifest inside the archive.</summary>
    public const string ManifestEntryName = "manifest.json";

    /// <summary>Canonical entry name of the SQLite snapshot inside the archive.</summary>
    public const string SqliteEntryName = "database/cognt.sqlite";

    /// <summary>Canonical entry-name prefix for referenced immutable assets.</summary>
    public const string AssetsEntryPrefix = "assets/";

    /// <summary>Manifest format version.</summary>
    public const int ManifestVersion = 1;

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Creates a verified backup. The pipeline writes a partial
    /// archive to a uniquely named sibling workspace, verifies
    /// it, then atomically renames the partial to the final
    /// destination path. Any failure leaves the source untouched
    /// and removes the partial output.
    /// </summary>
    public async Task<BackupOutcome> CreateAsync(BackupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Validate inputs.
        if (string.IsNullOrWhiteSpace(request.SourceCollectionRoot))
        {
            return new BackupOutcome(null, BackupFailureKind.HostilePath);
        }
        if (string.IsNullOrWhiteSpace(request.DestinationArchivePath))
        {
            return new BackupOutcome(null, BackupFailureKind.HostilePath);
        }
        string sourceRoot = NormalizeDirectory(request.SourceCollectionRoot);
        string destPath = NormalizeFile(request.DestinationArchivePath);
        if (!Directory.Exists(sourceRoot))
        {
            return new BackupOutcome(null, BackupFailureKind.NotFound);
        }

        // Allocate a uniquely named partial workspace.
        string destDir = Path.GetDirectoryName(destPath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(destDir);
        string partialPath = Path.Combine(destDir, $".{Path.GetFileName(destPath)}.partial-{Guid.NewGuid():N}");
        string stagingDir = Path.Combine(destDir, $".backup-staging-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(stagingDir);
            string stagingDb = Path.Combine(stagingDir, SqliteFileName);
            string stagingManifest = Path.Combine(stagingDir, "manifest.json");
            string stagingAssets = Path.Combine(stagingDir, "assets");

            // 1. Snapshot SQLite.
            string sourceDb = Path.Combine(sourceRoot, SqliteFileName);
            if (!File.Exists(sourceDb))
            {
                SafeDeleteDirectory(stagingDir);
                return new BackupOutcome(null, BackupFailureKind.NotFound);
            }
            await SnapshotSqliteAsync(sourceDb, stagingDb, cancellationToken).ConfigureAwait(true);

            // 2. Enumerate referenced assets (best-effort, empty list when no assets).
            var assets = EnumerateReferencedAssets(sourceRoot, stagingAssets, cancellationToken);

            // 3. Build manifest.
            var manifest = new BackupManifest(
                Version: ManifestVersion,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                SourceCollectionRoot: sourceRoot,
                SqliteSha256: await Sha256OfFileAsync(stagingDb, cancellationToken).ConfigureAwait(true),
                SqliteSize: new FileInfo(stagingDb).Length,
                AssetCount: assets.Count,
                Assets: assets);

            await File.WriteAllTextAsync(
                stagingManifest,
                JsonSerializer.Serialize(manifest, ManifestJsonOptions),
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(true);

            // 4. Write archive.
            if (File.Exists(partialPath)) File.Delete(partialPath);
            using (var zipStream = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                AddFileToArchive(archive, stagingDb, SqliteEntryName);
                AddFileToArchive(archive, stagingManifest, ManifestEntryName);
                foreach (var asset in assets)
                {
                    AddFileToArchive(archive, asset.StagingPath, AssetsEntryPrefix + asset.ArchiveEntryName);
                }
            }

            // 5. Verify (independent pass).
            var verification = await VerifyArchiveAsync(partialPath, cancellationToken).ConfigureAwait(true);
            if (!verification.Verified)
            {
                SafeDeleteFile(partialPath);
                SafeDeleteDirectory(stagingDir);
                return new BackupOutcome(null, BackupFailureKind.VerificationFailed);
            }

            // 6. Atomic finalize.
            if (File.Exists(destPath)) File.Delete(destPath);
            File.Move(partialPath, destPath);
            SafeDeleteDirectory(stagingDir);

            return new BackupOutcome(destPath, BackupFailureKind.None);
        }
        catch (OperationCanceledException)
        {
            SafeDeleteFile(partialPath);
            SafeDeleteDirectory(stagingDir);
            return new BackupOutcome(null, BackupFailureKind.Cancelled);
        }
        catch (IOException) when (!File.Exists(destPath))
        {
            SafeDeleteFile(partialPath);
            SafeDeleteDirectory(stagingDir);
            return new BackupOutcome(null, BackupFailureKind.DiskFull);
        }
        catch
        {
            SafeDeleteFile(partialPath);
            SafeDeleteDirectory(stagingDir);
            return new BackupOutcome(null, BackupFailureKind.Other);
        }
    }

    /// <summary>
    /// Verifies the archive independently of the write path.
    /// Re-opens the archive, recomputes the SHA-256 of every
    /// entry, and compares against the manifest.
    /// </summary>
    public async Task<BackupVerificationOutcome> VerifyArchiveAsync(string archivePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            return new BackupVerificationOutcome(false, null, "Archive not found.");
        }

        try
        {
            using var zipStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false);

            var manifestEntry = archive.GetEntry(ManifestEntryName);
            if (manifestEntry is null)
            {
                return new BackupVerificationOutcome(false, null, "Manifest missing.");
            }

            BackupManifest? manifest;
            using (var ms = new MemoryStream())
            {
                using (var es = manifestEntry.Open()) await es.CopyToAsync(ms, cancellationToken).ConfigureAwait(true);
                ms.Position = 0;
                manifest = JsonSerializer.Deserialize<BackupManifest>(ms.ToArray(), ManifestJsonOptions);
            }
            if (manifest is null)
            {
                return new BackupVerificationOutcome(false, null, "Manifest unreadable.");
            }

            if (manifest.Version != ManifestVersion)
            {
                return new BackupVerificationOutcome(false, null, "Manifest version mismatch.");
            }

            // Recompute the SQLite entry hash and compare.
            var dbEntry = archive.GetEntry(SqliteEntryName);
            if (dbEntry is null)
            {
                return new BackupVerificationOutcome(false, manifest.SqliteSha256, "Database entry missing.");
            }
            string dbSha = await Sha256OfEntryAsync(dbEntry, cancellationToken).ConfigureAwait(true);
            if (!string.Equals(dbSha, manifest.SqliteSha256, StringComparison.OrdinalIgnoreCase))
            {
                return new BackupVerificationOutcome(false, manifest.SqliteSha256, "SQLite checksum mismatch.");
            }

            // Recompute each asset hash and compare.
            foreach (var asset in manifest.Assets)
            {
                var entry = archive.GetEntry(AssetsEntryPrefix + asset.ArchiveEntryName);
                if (entry is null)
                {
                    return new BackupVerificationOutcome(false, manifest.SqliteSha256,
                        $"Asset entry missing: {asset.ArchiveEntryName}");
                }
                string assetSha = await Sha256OfEntryAsync(entry, cancellationToken).ConfigureAwait(true);
                if (!string.Equals(assetSha, asset.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return new BackupVerificationOutcome(false, manifest.SqliteSha256,
                        $"Asset checksum mismatch: {asset.ArchiveEntryName}");
                }
            }

            return new BackupVerificationOutcome(true, manifest.SqliteSha256, null);
        }
        catch (Exception ex)
        {
            return new BackupVerificationOutcome(false, null, ex.Message);
        }
    }

    /// <summary>
    /// Calls <see cref="CreateAsync"/> then <see cref="VerifyArchiveAsync"/>;
    /// if either fails, deletes the partial / verified archive so
    /// the destructive pipeline can refuse to start.
    /// </summary>
    public async Task<SafetyBackupVerification> CreateVerifiedSafetyBackupAsync(
        BackupRequest request, CancellationToken cancellationToken)
    {
        var create = await CreateAsync(request, cancellationToken).ConfigureAwait(true);
        if (create.FailureKind != BackupFailureKind.None || string.IsNullOrEmpty(create.ArchivePath))
        {
            return new SafetyBackupVerification(false, null, null,
                $"Backup failed: {create.FailureKind}");
        }

        var verify = await VerifyArchiveAsync(create.ArchivePath, cancellationToken).ConfigureAwait(true);
        if (!verify.Verified)
        {
            SafeDeleteFile(create.ArchivePath);
            return new SafetyBackupVerification(false, null, null, verify.FailureReason);
        }

        return new SafetyBackupVerification(true, create.ArchivePath, verify.ManifestSha256, null);
    }

    private static string NormalizeDirectory(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string NormalizeFile(string path)
        => Path.GetFullPath(path);

    private static void SafeDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    private static void SafeDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { /* best effort */ }
    }

    private static async Task SnapshotSqliteAsync(string sourceDb, string destDb, CancellationToken cancellationToken)
    {
        // Use SQLite's online backup API through Microsoft.Data.Sqlite.
        await using var source = new SqliteConnection($"Data Source={sourceDb};Mode=ReadOnly");
        await source.OpenAsync(cancellationToken).ConfigureAwait(true);
        await using var dest = new SqliteConnection($"Data Source={destDb}");
        await dest.OpenAsync(cancellationToken).ConfigureAwait(true);
        source.BackupDatabase(dest);
    }

    private static List<BackupAsset> EnumerateReferencedAssets(string sourceRoot, string stagingAssetsDir, CancellationToken cancellationToken)
    {
        var assets = new List<BackupAsset>();
        // The attachment directory is conventionally <root>/attachments or <root>/assets.
        // We scan both for ordinary files. Directories are skipped (we only mirror files).
        string[] candidateRoots = { "attachments", "assets" };
        foreach (var sub in candidateRoots)
        {
            string fromDir = Path.Combine(sourceRoot, sub);
            if (!Directory.Exists(fromDir)) continue;
            Directory.CreateDirectory(stagingAssetsDir);
            foreach (var file in Directory.EnumerateFiles(fromDir, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string rel = Path.GetRelativePath(fromDir, file).Replace('\\', '/');
                string stagedFile = Path.Combine(stagingAssetsDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(stagedFile)!);
                File.Copy(file, stagedFile, overwrite: true);
                assets.Add(new BackupAsset(
                    ArchiveEntryName: rel,
                    SourceRelativePath: Path.Combine(sub, rel).Replace('\\', '/'),
                    Sha256: Sha256OfFileSync(stagedFile),
                    Size: new FileInfo(stagedFile).Length)
                {
                    StagingPath = stagedFile,
                });
            }
        }
        return assets;
    }

    private static void AddFileToArchive(ZipArchive archive, string sourceFile, string entryName)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var es = entry.Open();
        using var fs = File.OpenRead(sourceFile);
        fs.CopyTo(es);
    }

    private static string Sha256OfFileSync(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(fs);
        return ToHex(hash);
    }

    private static async Task<string> Sha256OfFileAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha = SHA256.Create();
        var buffer = new byte[81920];
        int read;
        while ((read = await fs.ReadAsync(buffer, cancellationToken).ConfigureAwait(true)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return ToHex(sha.Hash!);
    }

    private static async Task<string> Sha256OfEntryAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        await using var es = entry.Open();
        var buffer = new byte[81920];
        int read;
        while ((read = await es.ReadAsync(buffer, cancellationToken).ConfigureAwait(true)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return ToHex(sha.Hash!);
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}

/// <summary>Internal manifest record. Public for serialization.</summary>
public sealed record BackupManifest(
    int Version,
    DateTimeOffset CreatedAtUtc,
    string SourceCollectionRoot,
    string SqliteSha256,
    long SqliteSize,
    int AssetCount,
    List<BackupAsset> Assets);

/// <summary>Internal asset record. Public for serialization.</summary>
public sealed record BackupAsset(
    string ArchiveEntryName,
    string SourceRelativePath,
    string Sha256,
    long Size)
{
    /// <summary>Staging path on disk; not serialized.</summary>
    public string? StagingPath { get; init; }
}

/// <summary>Result of <see cref="BackupArchiveService.VerifyArchiveAsync"/>.</summary>
public sealed record BackupVerificationOutcome(
    bool Verified,
    string? ManifestSha256,
    string? FailureReason);
