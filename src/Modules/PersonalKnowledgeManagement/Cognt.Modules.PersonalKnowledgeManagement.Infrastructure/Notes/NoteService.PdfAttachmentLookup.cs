using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static IReadOnlyDictionary<string, AttachmentRow> LoadPdfAttachmentsById(ITreeRepository repository)
    {
        return repository
            .LoadAttachments()
            .Where(row => InferAttachmentKind(row.FileExtension, row.ContentType) == KnowledgeAttachmentKind.Pdf)
            .ToDictionary(row => row.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<(NoteAttachmentRow Reference, AttachmentRow Attachment)> LoadAttachedPdfTreeSources(
        ITreeRepository repository,
        IReadOnlyDictionary<string, AttachmentRow> pdfAttachmentsById)
    {
        return repository
            .LoadNoteAttachments()
            .Where(reference => pdfAttachmentsById.ContainsKey(reference.AttachmentId))
            .Where(reference => repository.GetItem(reference.NoteItemId) is not null)
            .Select(reference => (Reference: reference, Attachment: pdfAttachmentsById[reference.AttachmentId]))
            .ToArray();
    }

    private static IReadOnlyList<AttachmentRow> LoadNotePdfAttachments(
        ITreeRepository repository,
        long noteItemId)
    {
        IReadOnlyDictionary<string, AttachmentRow> attachmentsById = repository
            .LoadAttachments()
            .ToDictionary(row => row.Id, StringComparer.OrdinalIgnoreCase);

        return repository
            .LoadNoteAttachments()
            .Where(reference => reference.NoteItemId == noteItemId)
            .Select(reference => attachmentsById.TryGetValue(reference.AttachmentId, out AttachmentRow? attachment) ? attachment : null)
            .Where(attachment => attachment is not null
                && InferAttachmentKind(attachment.FileExtension, attachment.ContentType) == KnowledgeAttachmentKind.Pdf)
            .Cast<AttachmentRow>()
            .ToArray();
    }
}
