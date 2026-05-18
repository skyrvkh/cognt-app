using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    async Task<KnowledgeAttachmentReadModel> INoteService.ImportAttachmentAsync(
        string collectionId,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken)
    {
        return await ImportAttachmentIntoCollectionStorageAsync(collectionId, sourceFileName, sourceContent, cancellationToken);
    }

    private async Task<ImportedAttachmentContext> ImportAttachmentIntoCollectionAsync(
        string collectionId,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken)
    {
        string root = GetExistingCollectionRoot(collectionId);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        KnowledgeAttachmentReadModel attachment = await ImportAttachmentIntoCollectionStorageAsync(
            collectionId,
            sourceFileName,
            sourceContent,
            cancellationToken);
        return new ImportedAttachmentContext(root, repository, attachment);
    }

    private async Task<KnowledgeAttachmentReadModel> ImportAttachmentIntoCollectionStorageAsync(
        string collectionId,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken)
    {
        PreparedAttachmentImport preparedImport = await PrepareAttachmentImportAsync(
            collectionId,
            sourceFileName,
            sourceContent,
            cancellationToken);
        if (preparedImport.ExistingAttachment is not null)
        {
            return await ReuseStoredAttachmentAsync(
                preparedImport.Root,
                preparedImport.Repository,
                preparedImport.ExistingAttachment,
                sourceContent,
                preparedImport.Sha256,
                preparedImport.ImportedAtUnixTime,
                cancellationToken);
        }

        return await PersistImportedAttachmentAsync(
            preparedImport.Root,
            preparedImport.Repository,
            sourceContent,
            preparedImport.Sha256,
            preparedImport.OriginalFileName,
            preparedImport.StoredFileRelativePath,
            preparedImport.StoredFilePath,
            preparedImport.ContentType,
            preparedImport.FileExtension,
            preparedImport.SizeBytes,
            preparedImport.ImportedAtUnixTime,
            cancellationToken);
    }

    private sealed class ImportedAttachmentContext
    {
        internal ImportedAttachmentContext(
            string root,
            ITreeRepository repository,
            KnowledgeAttachmentReadModel attachment)
        {
            Root = root;
            Repository = repository;
            Attachment = attachment;
        }

        internal string Root { get; }

        internal ITreeRepository Repository { get; }

        internal KnowledgeAttachmentReadModel Attachment { get; }
    }
}
