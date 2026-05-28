using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static async Task<KnowledgeAttachmentReadModel> ReuseStoredAttachmentAsync(
        PreparedAttachmentImport preparedImport,
        Stream sourceContent,
        CancellationToken cancellationToken,
        ILogger<NoteService>? logger = null)
    {
        AttachmentRow existing = preparedImport.ExistingAttachment
            ?? throw new InvalidOperationException("An existing attachment row is required for attachment reuse.");
        KnowledgeAttachmentKind kind = InferAttachmentKind(existing.FileExtension, existing.ContentType);
        string canonicalRelPath = CreateAttachmentRelPath(preparedImport.Sha256, existing.FileExtension, kind);
        string canonicalPath = GetStoredAttachmentPath(preparedImport.Root, canonicalRelPath);
        string existingPath = GetStoredAttachmentPath(preparedImport.Root, existing.RelativePath);
        if (!File.Exists(canonicalPath) && File.Exists(existingPath))
        {
            await CopyAttachmentFileAsync(sourceContent, canonicalPath, cancellationToken);
            logger?.LogWarning(
                "Recreated missing canonical attachment file during reuse. Root={Root} AttachmentId={AttachmentId} CanonicalRelativePath={CanonicalRelativePath}",
                preparedImport.Root,
                existing.Id,
                canonicalRelPath);
        }

        AttachmentRow updated = File.Exists(canonicalPath)
            ? CreateCanonicalAttachmentRow(existing, canonicalRelPath, preparedImport.ImportedAtUnixTime)
            : await RestoreMissingCanonicalAttachmentAsync(
                preparedImport.Root,
                existing,
                sourceContent,
                preparedImport.Sha256,
                preparedImport.ImportedAtUnixTime,
                cancellationToken,
                logger);

        preparedImport.Repository.UpsertAttachment(updated);
        return ToAttachmentReadModel(preparedImport.Root, updated);
    }

    private static async Task<KnowledgeAttachmentReadModel> PersistImportedAttachmentAsync(
        PreparedAttachmentImport preparedImport,
        Stream sourceContent,
        CancellationToken cancellationToken,
        ILogger<NoteService>? logger = null)
    {
        bool copied = false;
        try
        {
            await CopyAttachmentFileAsync(sourceContent, preparedImport.StoredFilePath, cancellationToken);
            copied = true;

            AttachmentRow row = new(
                CreateAttachmentId(preparedImport.Sha256),
                preparedImport.Sha256,
                preparedImport.OriginalFileName,
                Path.GetFileName(preparedImport.StoredFileRelativePath),
                preparedImport.StoredFileRelativePath,
                preparedImport.ContentType,
                preparedImport.FileExtension,
                preparedImport.SizeBytes,
                preparedImport.ImportedAtUnixTime,
                preparedImport.ImportedAtUnixTime);
            preparedImport.Repository.UpsertAttachment(row);
            return ToAttachmentReadModel(preparedImport.Root, row);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Imported attachment persistence failed; attempting file rollback. Root={Root} StoredFileRelativePath={StoredFileRelativePath} StoredFilePath={StoredFilePath} Copied={Copied}",
                preparedImport.Root,
                preparedImport.StoredFileRelativePath,
                preparedImport.StoredFilePath,
                copied);

            if (copied)
            {
                TryDeleteFile(preparedImport.StoredFilePath, logger);
            }

            throw;
        }
    }
}
