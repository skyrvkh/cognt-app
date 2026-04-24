using System.IO;
using System.Security.Cryptography;
using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static string CreateAttachmentId(string sha256)
    {
        return $"att-{sha256[..Math.Min(32, sha256.Length)]}";
    }

    private static string CreateNoteAttachmentId(
        string noteDocumentId,
        string attachmentId)
    {
        string input = string.Join('|', noteDocumentId, attachmentId);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static string CreateAttachmentRelPath(
        string sha256,
        string extension,
        KnowledgeAttachmentKind kind)
    {
        string normalizedExtension = NormalizeAttachmentExtension(extension);
        string fileName = $"{sha256}{normalizedExtension}";
        string relativePath = kind == KnowledgeAttachmentKind.Image
            ? Path.Combine(AttachmentsDirectoryName, ImagesDirectoryName, fileName)
            : Path.Combine(AttachmentsDirectoryName, fileName);

        return NormalizeRelativePath(relativePath);
    }

    private static string NormalizeAttachmentExtension(string extension)
    {
        string normalized = string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : extension.Trim().ToLowerInvariant();

        if (normalized.Length == 0)
        {
            throw new InvalidOperationException("Attachment file extension is required.");
        }

        if (normalized[0] != '.'
            || normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || normalized.Contains(Path.DirectorySeparatorChar)
            || normalized.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException("Attachment file extension is invalid.");
        }

        return normalized;
    }

    private static string GetStoredAttachmentPath(string root, string relativePath)
    {
        string fullPath = Path.GetFullPath(Path.Combine(root, ToPlatformPath(relativePath)));
        if (!IsPathInsideRoot(fullPath, root))
        {
            throw new UnauthorizedAccessException("The stored attachment path is outside the open collection root.");
        }

        return fullPath;
    }

    private static KnowledgeAttachmentReadModel ToAttachmentReadModel(
        string root,
        AttachmentRow row)
    {
        return new KnowledgeAttachmentReadModel(
            row.Id,
            row.OriginalFileName,
            NormalizeRelativePath(row.RelativePath),
            InferAttachmentKind(row.FileExtension, row.ContentType));
    }

}
