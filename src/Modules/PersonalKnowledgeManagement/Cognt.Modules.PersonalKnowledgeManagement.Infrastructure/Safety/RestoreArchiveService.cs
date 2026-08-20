using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Safety;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Safety;

/// <summary>
/// Owner of the verified restore pipeline. The service treats
/// the supplied archive as hostile: it enforces entry / count /
/// size / ratio bounds, extracts to a disposable child,
/// verifies manifest + checksums + database + assets, then
/// registers the restored collection without in-place overwrite.
/// </summary>
public sealed class RestoreArchiveService
{
    /// <summary>Maximum archive size in bytes (default 4 GiB).</summary>
    public long MaxArchiveBytes { get; init; } = 4L * 1024 * 1024 * 1024;

    /// <summary>Maximum number of entries (default 200,000).</summary>
    public int MaxEntryCount { get; init; } = 200_000;

    /// <summary>Maximum total payload size after decompression (default 32 GiB).</summary>
    public long MaxTotalBytes { get; init; } = 32L * 1024 * 1024 * 1024;

    /// <summary>Compression ratio ceiling (decompressed / compressed).</summary>
    public double MaxCompressionRatio { get; init; } = 100.0;

    private const string ManifestEntryName = BackupArchiveService.ManifestEntryName;
    private const string SqliteEntryName = BackupArchiveService.SqliteEntryName;
    private const string AssetsEntryPrefix = BackupArchiveService.AssetsEntryPrefix;
    private const string SqliteFileName = BackupArchiveService.SqliteFileName;

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Performs the restore. The pipeline:
    /// 1. Validates the archive size, entry count, and compression ratio.
    /// 2. Extracts the archive to a disposable child directory.
    /// 3. Verifies the manifest + entry checksums (delegating to
    ///    <see cref="BackupArchiveService.VerifyArchiveAsync"/>).
    /// 4. Verifies the SQLite database passes integrity_check.
    /// 5. Verifies the manifest-declared assets are present and
    ///    checksum-valid.
    /// 6. Registers the restored collection (moves the disposable
    ///    child to the target collection root) and returns success.
    /// </summary>
    public async Task<RestoreOutcome> RestoreAsync(
        RestoreRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ArchivePath) || !File.Exists(request.ArchivePath))
        {
            return new RestoreOutcome(RestoreFailureKind.ArchiveInvalid);
        }
        if (string.IsNullOrWhiteSpace(request.TargetCollectionRoot))
        {
            return new RestoreOutcome(RestoreFailureKind.HostilePath);
        }

        string archivePath = Path.GetFullPath(request.ArchivePath);
        string targetRoot = Path.GetFullPath(request.TargetCollectionRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // 1. Containment + size check.
        long size = new FileInfo(archivePath).Length;
        if (size > MaxArchiveBytes)
        {
            return new RestoreOutcome(RestoreFailureKind.ArchiveInvalid);
        }

        // 2. Extract to disposable child.
        string extractDir = Path.Combine(
            Path.GetDirectoryName(targetRoot) ?? Path.GetTempPath(),
            $".restore-staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(extractDir);

        try
        {
            using (var zipStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false))
            {
                if (archive.Entries.Count > MaxEntryCount)
                {
                    SafeDeleteDirectory(extractDir);
                    return new RestoreOutcome(RestoreFailureKind.ArchiveInvalid);
                }

                long totalBytes = 0;
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(entry.Name) || entry.Name.Contains("..", StringComparison.Ordinal))
                    {
                        SafeDeleteDirectory(extractDir);
                        return new RestoreOutcome(RestoreFailureKind.HostilePath);
                    }
                    long entryCompressed = entry.CompressedLength;
                    if (entryCompressed > 0 && entry.Length > 0)
                    {
                        double ratio = (double)entry.Length / Math.Max(1, entryCompressed);
                        if (ratio > MaxCompressionRatio)
                        {
                            SafeDeleteDirectory(extractDir);
                            return new RestoreOutcome(RestoreFailureKind.ArchiveInvalid);
                        }
                    }
                    totalBytes += entry.Length;
                    if (totalBytes > MaxTotalBytes)
                    {
                        SafeDeleteDirectory(extractDir);
                        return new RestoreOutcome(RestoreFailureKind.ArchiveInvalid);
                    }

                    string outPath = Path.Combine(extractDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                    string outDir = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
                    using var es = entry.Open();
                    using var fs = File.Create(outPath);
                    await es.CopyToAsync(fs, cancellationToken).ConfigureAwait(true);
                }
            }

            // 3. Verify the archive.
            var verifier = new BackupArchiveService();
            var verify = await verifier.VerifyArchiveAsync(archivePath, cancellationToken).ConfigureAwait(true);
            if (!verify.Verified)
            {
                SafeDeleteDirectory(extractDir);
                return new RestoreOutcome(RestoreFailureKind.ChecksumMismatch);
            }

            // 4. SQLite integrity check.
            string dbPath = Path.Combine(extractDir, SqliteEntryName.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(dbPath))
            {
                SafeDeleteDirectory(extractDir);
                return new RestoreOutcome(RestoreFailureKind.SchemaInvalid);
            }
            if (!SqliteQuickCheck(dbPath))
            {
                SafeDeleteDirectory(extractDir);
                return new RestoreOutcome(RestoreFailureKind.SchemaInvalid);
            }

            // 5. Manifest asset presence (already done by VerifyArchiveAsync; double-check on disk).
            var manifestJson = await File.ReadAllTextAsync(
                Path.Combine(extractDir, ManifestEntryName), cancellationToken).ConfigureAwait(true);
            var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson, ManifestJsonOptions);
            if (manifest is null)
            {
                SafeDeleteDirectory(extractDir);
                return new RestoreOutcome(RestoreFailureKind.ArchiveInvalid);
            }
            foreach (var asset in manifest.Assets)
            {
                string staged = Path.Combine(extractDir, AssetsEntryPrefix.Replace('/', Path.DirectorySeparatorChar), asset.ArchiveEntryName);
                if (!File.Exists(staged))
                {
                    SafeDeleteDirectory(extractDir);
                    return new RestoreOutcome(RestoreFailureKind.ChecksumMismatch);
                }
            }

            // 6. Register without in-place overwrite.
            if (Directory.Exists(targetRoot))
            {
                SafeDeleteDirectory(extractDir);
                return new RestoreOutcome(RestoreFailureKind.HostilePath);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(targetRoot)!);
            Directory.Move(extractDir, targetRoot);

            return new RestoreOutcome(RestoreFailureKind.None);
        }
        catch (OperationCanceledException)
        {
            SafeDeleteDirectory(extractDir);
            return new RestoreOutcome(RestoreFailureKind.Cancelled);
        }
        catch
        {
            SafeDeleteDirectory(extractDir);
            return new RestoreOutcome(RestoreFailureKind.Other);
        }
    }

    private static bool SqliteQuickCheck(string dbPath)
    {
        try
        {
            // Use the lightweight Microsoft.Data.Sqlite available in the infrastructure project.
            var connectionString = $"Data Source={dbPath};Mode=ReadOnly";
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var result = cmd.ExecuteScalar() as string;
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void SafeDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { /* best effort */ }
    }
}
