using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    async Task<KnowledgePdfDocumentReadModel> INoteService.ImportPdfDocumentAsync(
        string collectionId,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken)
    {
        ImportedAttachmentContext imported = await ImportAttachmentIntoCollectionAsync(
            collectionId,
            sourceFileName,
            sourceContent,
            cancellationToken);

        if (imported.Attachment.Kind != KnowledgeAttachmentKind.Pdf)
        {
            throw new InvalidOperationException("The selected attachment is not a PDF.");
        }

        EnsureImportedAttachmentTreeState(
            imported.Repository,
            imported.Attachment,
            parentId: null,
            imported.Root);

        KnowledgePdfDocumentReadModel pdfDocument = ToPdfReadModel(imported.Attachment);
        return pdfDocument;
    }

    async Task<string> INoteService.AttachFileToNoteAsync(
        string collectionId,
        string noteRelativePath,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken)
    {
        NoteAttachmentRow reference = await CreateNoteAttachmentReferenceAsync(
            collectionId,
            noteRelativePath,
            sourceFileName,
            sourceContent,
            cancellationToken);

        return reference.DisplayName;
    }

    async Task<KnowledgeAttachmentReadModel> INoteService.AttachFileReferenceToNoteAsync(
        string collectionId,
        string noteRelativePath,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken)
    {
        NoteAttachmentRow reference = await CreateNoteAttachmentReferenceAsync(
            collectionId,
            noteRelativePath,
            sourceFileName,
            sourceContent,
            cancellationToken);

        string root = GetExistingCollectionRoot(collectionId);
        AttachmentRow row = OpenTreeRepositoryForCollectionPath(root).GetAttachment(reference.AttachmentId)
            ?? throw new InvalidOperationException("The attached file could not be found after import.");

        KnowledgeAttachmentReadModel attachment = ToAttachmentReadModel(root, row);
        return attachment;
    }
}
