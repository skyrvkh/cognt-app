using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    public async Task<KnowledgePdfDocumentReadModel> ImportPdfDocumentAsync(
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

        return ToPdfReadModel(imported.Attachment);
    }

    public async Task<string> AttachFileToNoteAsync(
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

    public async Task<KnowledgeAttachmentReadModel> AttachFileReferenceToNoteAsync(
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

        return ToAttachmentReadModel(root, row);
    }
}
