using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static void EnsureAllPdfAttachmentTreeItems(
        ITreeRepository repository,
        bool preserveDeletedTreeItems)
    {
        IReadOnlyDictionary<string, AttachmentRow> pdfAttachmentsById = LoadPdfAttachmentsById(repository);
        if (pdfAttachmentsById.Count == 0)
        {
            return;
        }

        HashSet<string> attachedPdfIds = new(StringComparer.OrdinalIgnoreCase);

        foreach ((NoteAttachmentRow Reference, AttachmentRow Attachment) attachedPdf in LoadAttachedPdfTreeSources(repository, pdfAttachmentsById))
        {
            attachedPdfIds.Add(attachedPdf.Reference.AttachmentId);
            EnsurePdfAttachmentTreeItem(
                repository,
                attachedPdf.Attachment,
                attachedPdf.Reference.NoteItemId,
                preserveDeletedTreeItems);
        }

        foreach (AttachmentRow attachment in pdfAttachmentsById.Values)
        {
            if (attachedPdfIds.Contains(attachment.Id))
            {
                continue;
            }

            EnsurePdfAttachmentTreeItem(repository, attachment, parentId: null, preserveDeletedTreeItems);
        }
    }

    private static void EnsureNotePdfAttachmentTreeItems(
        ITreeRepository repository,
        long noteItemId,
        bool preserveDeletedTreeItems)
    {
        IReadOnlyList<AttachmentRow> notePdfAttachments = LoadNotePdfAttachments(repository, noteItemId);
        if (notePdfAttachments.Count == 0)
        {
            return;
        }

        foreach (AttachmentRow attachment in notePdfAttachments)
        {
            EnsurePdfAttachmentTreeItem(repository, attachment, noteItemId, preserveDeletedTreeItems);
        }
    }

    private static KnowledgeTreeItemRow EnsurePdfAttachmentTreeItem(
        ITreeRepository repository,
        AttachmentRow attachment,
        long? parentId,
        bool preserveDeletedTreeItems)
    {
        string relativePath = NormalizeRelativePath(attachment.RelativePath);
        KnowledgeTreeItemRow? existing = repository.FindItemByFileRelPath(
            relativePath,
            parentId,
            KnowledgeTreeItemKind.PdfDocument,
            includeDeleted: preserveDeletedTreeItems);

        if (existing is not null)
        {
            return existing;
        }

        long itemId = repository.CreatePdfDocument(
            parentId,
            attachment.OriginalFileName,
            relativePath);

        return repository.GetItem(itemId)
            ?? throw new InvalidOperationException("The imported PDF tree item could not be created.");
    }
}
