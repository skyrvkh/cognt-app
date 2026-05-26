using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static IReadOnlyDictionary<string, AttachmentRow> LoadPdfAttachmentsById(ITreeRepository repository)
    {
        IReadOnlyList<AttachmentRow> attachments = repository.LoadAttachments();
        Dictionary<string, AttachmentRow> pdfAttachmentsById = new(attachments.Count, StringComparer.OrdinalIgnoreCase);
        foreach (AttachmentRow row in attachments)
        {
            if (InferAttachmentKind(row.FileExtension, row.ContentType) == KnowledgeAttachmentKind.Pdf)
            {
                pdfAttachmentsById.Add(row.Id, row);
            }
        }

        return pdfAttachmentsById;
    }

    private static IReadOnlyList<(NoteAttachmentRow Reference, AttachmentRow Attachment)> LoadAttachedPdfTreeSources(
        ITreeRepository repository,
        IReadOnlyDictionary<string, AttachmentRow> pdfAttachmentsById)
    {
        IReadOnlyList<NoteAttachmentRow> references = repository.LoadNoteAttachments();
        List<(NoteAttachmentRow Reference, AttachmentRow Attachment)> attachedSources = [];
        foreach (NoteAttachmentRow reference in references)
        {
            if (!pdfAttachmentsById.TryGetValue(reference.AttachmentId, out AttachmentRow? attachment)
                || repository.GetItem(reference.NoteItemId) is null)
            {
                continue;
            }

            attachedSources.Add((reference, attachment));
        }

        return attachedSources.ToArray();
    }

    private static IReadOnlyList<AttachmentRow> LoadNotePdfAttachments(
        ITreeRepository repository,
        long noteItemId)
    {
        IReadOnlyList<AttachmentRow> attachments = repository.LoadAttachments();
        Dictionary<string, AttachmentRow> attachmentsById = new(attachments.Count, StringComparer.OrdinalIgnoreCase);
        foreach (AttachmentRow attachment in attachments)
        {
            attachmentsById.Add(attachment.Id, attachment);
        }

        List<AttachmentRow> noteAttachments = [];
        foreach (NoteAttachmentRow reference in repository.LoadNoteAttachments())
        {
            if (reference.NoteItemId != noteItemId
                || !attachmentsById.TryGetValue(reference.AttachmentId, out AttachmentRow? attachment)
                || InferAttachmentKind(attachment.FileExtension, attachment.ContentType) != KnowledgeAttachmentKind.Pdf)
            {
                continue;
            }

            noteAttachments.Add(attachment);
        }

        return noteAttachments.ToArray();
    }
}
