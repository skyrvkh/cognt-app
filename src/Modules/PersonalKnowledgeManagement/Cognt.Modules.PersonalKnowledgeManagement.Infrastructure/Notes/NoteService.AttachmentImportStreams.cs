using System.IO;
using System.Security.Cryptography;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static void EnsureImportSource(string sourceFileName, Stream sourceContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        ArgumentNullException.ThrowIfNull(sourceContent);

        if (!sourceContent.CanRead)
        {
            throw new ArgumentException("The import source stream must be readable.", nameof(sourceContent));
        }

        if (!sourceContent.CanSeek)
        {
            throw new ArgumentException("The import source stream must be seekable.", nameof(sourceContent));
        }
    }

    private static async Task<bool> HasPdfHeaderAsync(Stream sourceContent, CancellationToken cancellationToken)
    {
        ResetSourceStream(sourceContent);
        byte[] header = new byte[5];
        int read = await sourceContent.ReadAsync(header, cancellationToken);
        ResetSourceStream(sourceContent);

        return read == header.Length
            && header[0] == (byte)'%'
            && header[1] == (byte)'P'
            && header[2] == (byte)'D'
            && header[3] == (byte)'F'
            && header[4] == (byte)'-';
    }

    private static async Task<string> ComputeFileHashAsync(
        Stream sourceContent,
        CancellationToken cancellationToken)
    {
        ResetSourceStream(sourceContent);
        byte[] hash = await SHA256.HashDataAsync(sourceContent, cancellationToken);
        ResetSourceStream(sourceContent);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task CopyAttachmentFileAsync(
        Stream sourceContent,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        ResetSourceStream(sourceContent);
        await using FileStream destination = new(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 128,
            useAsync: true);
        await sourceContent.CopyToAsync(destination, cancellationToken);
        ResetSourceStream(sourceContent);
    }

    private static void ResetSourceStream(Stream sourceContent)
    {
        sourceContent.Position = 0;
    }
}
