using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static AttachmentRow CreateCanonicalAttachmentRow(
        AttachmentRow existing,
        string canonicalRelPath,
        long now)
    {
        return new AttachmentRow(
            existing.Id,
            existing.Sha256,
            existing.OriginalFileName,
            Path.GetFileName(canonicalRelPath),
            canonicalRelPath,
            existing.ContentType,
            existing.FileExtension,
            existing.SizeBytes,
            existing.CreatedAt,
            now);
    }

    private static async Task<AttachmentRow> RestoreMissingCanonicalAttachmentAsync(
        string root,
        AttachmentRow existing,
        Stream sourceContent,
        string sha256,
        long now,
        CancellationToken cancellationToken,
        ILogger<NoteService>? logger = null)
    {
        KnowledgeAttachmentKind existingKind = InferAttachmentKind(existing.FileExtension, existing.ContentType);
        string repairedRelPath = CreateAttachmentRelPath(sha256, existing.FileExtension, existingKind);
        string repairedPath = GetStoredAttachmentPath(root, repairedRelPath);
        await CopyAttachmentFileAsync(sourceContent, repairedPath, cancellationToken);
        logger?.LogWarning(
            "Restored missing canonical attachment file. Root={Root} AttachmentId={AttachmentId} RelativePath={RelativePath}",
            root,
            existing.Id,
            repairedRelPath);

        return new AttachmentRow(
            existing.Id,
            existing.Sha256,
            existing.OriginalFileName,
            Path.GetFileName(repairedRelPath),
            repairedRelPath,
            existing.ContentType,
            existing.FileExtension,
            existing.SizeBytes,
            existing.CreatedAt,
            now);
    }
}
