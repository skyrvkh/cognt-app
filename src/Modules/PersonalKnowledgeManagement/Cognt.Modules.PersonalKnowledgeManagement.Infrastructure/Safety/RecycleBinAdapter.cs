using System;
using System.Collections.Generic;
using System.IO;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Safety;

/// <summary>
/// Adapter that moves a source path into the platform Recycle
/// Bin. The adapter is the single owner of the destructive
/// move: the application layer never composes a shell command
/// and never invokes <c>Directory.Delete</c> with
/// <c>recursive: true</c> directly. The test-mode
/// <see cref="StagingRecycleBinAdapter"/> stages the move to a
/// sibling <c>.recycle-bin</c> directory; production-mode
/// implementations can wrap the Visual Basic
/// <c>FileIO.FileSystem.DeleteFile</c> helper or the Win32
/// <c>SHFileOperation</c> API.
/// </summary>
public interface IRecycleBinAdapter
{
    /// <summary>
    /// Moves the source path to the Recycle Bin. The
    /// implementation must validate the source is inside the
    /// supplied collection root, must never compose a shell
    /// command, and must throw
    /// <see cref="RecycleBinMoveException"/> on any failure.
    /// </summary>
    /// <param name="sourcePath">Absolute path of the source.</param>
    /// <param name="collectionRoot">Canonical collection root the source must be inside.</param>
    /// <returns>The single-line outcome the caller renders.</returns>
    string MoveToRecycleBin(string sourcePath, string collectionRoot);
}

/// <summary>
/// Outcome of a Recycle Bin move. The exception is the only
/// signal the pipeline can fail at; the message is the
/// single-line text the view-model renders.
/// </summary>
public sealed class RecycleBinMoveException : Exception
{
    /// <summary>Builds a typed failure exception.</summary>
    public RecycleBinMoveException(string message)
        : base(string.IsNullOrWhiteSpace(message) ? "The Recycle Bin move failed." : message)
    {
    }
}

/// <summary>
/// Test-mode Recycle Bin adapter. Stages the move to a sibling
/// <c>.recycle-bin</c> directory under the collection root so
/// the production-shaped surface is observable from a
/// non-Windows test environment.
/// </summary>
public sealed class StagingRecycleBinAdapter : IRecycleBinAdapter
{
    /// <summary>Name of the staging directory under the collection root.</summary>
    public const string StagingDirectoryName = ".recycle-bin";

    /// <inheritdoc />
    public string MoveToRecycleBin(string sourcePath, string collectionRoot)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new RecycleBinMoveException("The source path is empty.");
        }

        if (string.IsNullOrWhiteSpace(collectionRoot))
        {
            throw new RecycleBinMoveException("The collection root is empty.");
        }

        string normalizedSource = Path.GetFullPath(sourcePath);
        string normalizedRoot = Path.GetFullPath(collectionRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;
        if (!normalizedSource.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedSource, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new RecycleBinMoveException("The source path is outside the collection root.");
        }

        string stagingRoot = Path.Combine(normalizedRoot, StagingDirectoryName);
        Directory.CreateDirectory(stagingRoot);

        string destination = Path.Combine(
            stagingRoot,
            Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(normalizedSource));

        if (File.Exists(normalizedSource))
        {
            File.Move(normalizedSource, destination);
        }
        else if (Directory.Exists(normalizedSource))
        {
            Directory.Move(normalizedSource, destination);
        }
        else
        {
            throw new RecycleBinMoveException("The source path does not exist.");
        }

        return $"Recycle Bin move staged at {destination}.";
    }
}

/// <summary>
/// No-op Recycle Bin adapter. Records every call but does not
/// touch the filesystem. Used by the test-only single-arg
/// <see cref="TrashArchiveService"/> constructor when the test
/// does not exercise the destructive move.
/// </summary>
public sealed class NoOpRecycleBinAdapter : IRecycleBinAdapter
{
    /// <summary>List of source paths the adapter was asked to move.</summary>
    public List<string> MoveRequests { get; } = new();

    /// <inheritdoc />
    public string MoveToRecycleBin(string sourcePath, string collectionRoot)
    {
        MoveRequests.Add(sourcePath);
        return $"No-op Recycle Bin move for {sourcePath}.";
    }
}
