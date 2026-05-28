using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal static class NoteCatalogAttachmentIndex
{
    internal static IReadOnlyDictionary<long, IReadOnlyList<AttachmentRow>> LoadByNoteId(
        ITreeRepository repository)
    {
        IReadOnlyList<AttachmentRow> attachments = repository.LoadAttachments();

        Dictionary<string, AttachmentRow> attachmentsById = new(attachments.Count, StringComparer.OrdinalIgnoreCase);
        foreach (AttachmentRow attachment in attachments)
        {
            attachmentsById.Add(attachment.Id, attachment);
        }

        Dictionary<long, List<AttachmentRow>> mutableAttachmentsByNoteId = [];
        foreach (NoteAttachmentRow reference in repository.LoadNoteAttachments())
        {
            if (!attachmentsById.TryGetValue(reference.AttachmentId, out AttachmentRow? attachment))
            {
                continue;
            }

            if (!mutableAttachmentsByNoteId.TryGetValue(reference.NoteItemId, out List<AttachmentRow>? existing))
            {
                mutableAttachmentsByNoteId.Add(reference.NoteItemId, new List<AttachmentRow> { attachment });
                continue;
            }

            existing.Add(attachment);
        }

        Dictionary<long, IReadOnlyList<AttachmentRow>> attachmentsByNoteId = new(mutableAttachmentsByNoteId.Count);
        foreach (KeyValuePair<long, List<AttachmentRow>> pair in mutableAttachmentsByNoteId)
        {
            attachmentsByNoteId.Add(pair.Key, pair.Value);
        }

        return attachmentsByNoteId;
    }
}
