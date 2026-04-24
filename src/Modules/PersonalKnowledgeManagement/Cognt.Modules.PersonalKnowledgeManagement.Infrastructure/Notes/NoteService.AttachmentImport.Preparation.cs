using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private async Task<PreparedAttachmentImport> PrepareAttachmentImportAsync(
        string collectionId,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken)
    {
        EnsureImportSource(sourceFileName, sourceContent);

        string root = GetExistingCollectionRoot(collectionId);
        AttachmentClassification classification = await ValidateAndClassifyAttachmentImportAsync(
            sourceFileName,
            sourceContent,
            cancellationToken);
        string sha256 = await ComputeFileHashAsync(sourceContent, cancellationToken);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        AttachmentRow? existingAttachment = repository.FindAttachmentBySha256(sha256);
        long importedAtUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string originalFileName = Path.GetFileName(sourceFileName);
        string fileExtension = NormalizeAttachmentExtension(Path.GetExtension(originalFileName));
        string storedFileRelativePath = CreateAttachmentRelPath(sha256, fileExtension, classification.Kind);

        return new PreparedAttachmentImport(
            root,
            repository,
            existingAttachment,
            sha256,
            originalFileName,
            fileExtension,
            storedFileRelativePath,
            GetStoredAttachmentPath(root, storedFileRelativePath),
            classification.ContentType,
            sourceContent.Length,
            importedAtUnixTime);
    }

    private static async Task<AttachmentClassification> ValidateAndClassifyAttachmentImportAsync(
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken)
    {
        AttachmentClassification classification = ClassifyAttachment(sourceFileName);
        if (classification.Kind == KnowledgeAttachmentKind.Pdf
            && !await HasPdfHeaderAsync(sourceContent, cancellationToken))
        {
            throw new InvalidOperationException("The selected file does not look like a valid PDF.");
        }

        return classification;
    }

    private sealed class PreparedAttachmentImport
    {
        internal PreparedAttachmentImport(
            string root,
            ITreeRepository repository,
            AttachmentRow? existingAttachment,
            string sha256,
            string originalFileName,
            string fileExtension,
            string storedFileRelativePath,
            string storedFilePath,
            string contentType,
            long sizeBytes,
            long importedAtUnixTime)
        {
            Root = root;
            Repository = repository;
            ExistingAttachment = existingAttachment;
            Sha256 = sha256;
            OriginalFileName = originalFileName;
            FileExtension = fileExtension;
            StoredFileRelativePath = storedFileRelativePath;
            StoredFilePath = storedFilePath;
            ContentType = contentType;
            SizeBytes = sizeBytes;
            ImportedAtUnixTime = importedAtUnixTime;
        }

        internal string Root { get; }

        internal ITreeRepository Repository { get; }

        internal AttachmentRow? ExistingAttachment { get; }

        internal string Sha256 { get; }

        internal string OriginalFileName { get; }

        internal string FileExtension { get; }

        internal string StoredFileRelativePath { get; }

        internal string StoredFilePath { get; }

        internal string ContentType { get; }

        internal long SizeBytes { get; }

        internal long ImportedAtUnixTime { get; }
    }
}
