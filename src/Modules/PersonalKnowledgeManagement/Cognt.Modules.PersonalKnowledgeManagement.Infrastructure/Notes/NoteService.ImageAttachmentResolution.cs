using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static bool IsImageAttachment(AttachmentRow row)
    {
        return InferAttachmentKind(row.FileExtension, row.ContentType) == KnowledgeAttachmentKind.Image;
    }

    private static string NormalizeImageSource(string imageSource)
    {
        return Uri.UnescapeDataString(imageSource)
            .Replace('\\', '/')
            .Trim()
            .Trim('<', '>')
            .TrimStart('/');
    }

    private static bool ImageSourceMatchesAttachment(
        string normalizedSource,
        string? decodedSha256Source,
        AttachmentRow row)
    {
        return string.Equals(normalizedSource, row.RelativePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedSource, row.Id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedSource, row.Sha256, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedSource, row.StoredFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(decodedSha256Source, row.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetCollectionRelativeImagePath(string root, string normalizedSource)
    {
        if (Uri.TryCreate(normalizedSource, UriKind.Absolute, out Uri? absoluteUri))
        {
            if (!absoluteUri.IsFile)
            {
                return null;
            }

            string fullPath = Path.GetFullPath(absoluteUri.LocalPath);
            return TryGetCollectionRelativeExistingImagePath(root, fullPath);
        }

        string platformSource = ToPlatformPath(normalizedSource);
        string candidatePath = Path.IsPathRooted(platformSource)
            ? Path.GetFullPath(platformSource)
            : Path.GetFullPath(Path.Combine(root, platformSource));
        return TryGetCollectionRelativeExistingImagePath(root, candidatePath);
    }

    private static string? TryGetCollectionRelativeExistingImagePath(string root, string fullPath)
    {
        if (!IsPathInsideRoot(fullPath, root)
            || !File.Exists(fullPath)
            || !ImageAttachmentExtensions.Contains(Path.GetExtension(fullPath)))
        {
            return null;
        }

        return NormalizeRelativePath(Path.GetRelativePath(root, fullPath));
    }

    private static string? TryDecodeSha256SourceToken(string normalizedSource)
    {
        string token = normalizedSource.Trim();
        if (token.Length == 0 || token.Contains('/'))
        {
            return null;
        }

        string padded = token
            .Replace('-', '+')
            .Replace('_', '/');
        int padding = padded.Length % 4;
        if (padding > 0)
        {
            padded = padded.PadRight(padded.Length + 4 - padding, '=');
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(padded);
            return bytes.Length == 32
                ? Convert.ToHexString(bytes).ToLowerInvariant()
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
