using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static async Task<KnowledgeAttachmentReadModel> ReuseStoredAttachmentAsync(
        string root,
        ITreeRepository repository,
        AttachmentRow existing,
        Stream sourceContent,
        string sha256,
        long now,
        CancellationToken cancellationToken)
    {
        KnowledgeAttachmentKind kind = InferAttachmentKind(existing.FileExtension, existing.ContentType);
        string canonicalRelPath = CreateAttachmentRelPath(sha256, existing.FileExtension, kind);
        string canonicalPath = GetStoredAttachmentPath(root, canonicalRelPath);
        string existingPath = GetStoredAttachmentPath(root, existing.RelativePath);
        if (!File.Exists(canonicalPath) && File.Exists(existingPath))
        {
            await CopyAttachmentFileAsync(sourceContent, canonicalPath, cancellationToken);
        }

        AttachmentRow updated = File.Exists(canonicalPath)
            ? CreateCanonicalAttachmentRow(existing, canonicalRelPath, now)
            : await RestoreMissingCanonicalAttachmentAsync(root, existing, sourceContent, sha256, now, cancellationToken);

        repository.UpsertAttachment(updated);
        return ToAttachmentReadModel(root, updated);
    }

    private static async Task<KnowledgeAttachmentReadModel> PersistImportedAttachmentAsync(
        string root,
        ITreeRepository repository,
        Stream sourceContent,
        string sha256,
        string originalFileName,
        string storedFileRelPath,
        string storedFilePath,
        string contentType,
        string extension,
        long sizeBytes,
        long now,
        CancellationToken cancellationToken)
    {
        bool copied = false;
        try
        {
            await CopyAttachmentFileAsync(sourceContent, storedFilePath, cancellationToken);
            copied = true;

            AttachmentRow row = new(
                CreateAttachmentId(sha256),
                sha256,
                originalFileName,
                Path.GetFileName(storedFileRelPath),
                storedFileRelPath,
                contentType,
                extension,
                sizeBytes,
                now,
                now);
            repository.UpsertAttachment(row);
            return ToAttachmentReadModel(root, row);
        }
        catch
        {
            if (copied)
            {
                TryDeleteFile(storedFilePath);
            }

            throw;
        }
    }
}
