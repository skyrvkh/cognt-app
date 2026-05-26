using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    string? INoteService.ResolveImageAttachmentRelativePath(
        string collectionId,
        string noteRelativePath,
        string imageSource)
    {
        string root = GetExistingCollectionRoot(collectionId);
        string normalizedNoteRelPath = NormalizeRelativePath(noteRelativePath);
        string fullNotePath = ResolveExistingRelativeNotePathInRoot(root, normalizedNoteRelPath);
        string resolvedNoteRelPath = NormalizeRelativePath(Path.GetRelativePath(root, fullNotePath));
        string normalizedSource = NormalizeImageSource(imageSource);
        if (string.IsNullOrWhiteSpace(normalizedSource))
        {
            return null;
        }

        string? relativePath = TryGetCollectionRelativeImagePath(root, normalizedSource);
        if (relativePath is not null)
        {
            return relativePath;
        }

        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        KnowledgeTreeItemRow? note = repository.FindNoteByFileRelPath(resolvedNoteRelPath)
            ?? repository.FindNoteByFileRelPath(normalizedNoteRelPath);
        if (note is null)
        {
            return null;
        }

        IReadOnlyList<AttachmentRow> attachments = repository.LoadAttachments();
        Dictionary<string, AttachmentRow> attachmentsById = new(attachments.Count, StringComparer.OrdinalIgnoreCase);
        foreach (AttachmentRow attachment in attachments)
        {
            if (IsImageAttachment(attachment))
            {
                attachmentsById.Add(attachment.Id, attachment);
            }
        }

        int noteImageCount = 0;
        AttachmentRow? onlyImage = null;
        AttachmentRow? matched = null;
        foreach (NoteAttachmentRow reference in repository.LoadNoteAttachments())
        {
            if (reference.NoteItemId != note.Id
                || !attachmentsById.TryGetValue(reference.AttachmentId, out AttachmentRow? attachment))
            {
                continue;
            }

            noteImageCount++;
            onlyImage = noteImageCount == 1 ? attachment : null;
            if (matched is null && ImageSourceMatchesAttachment(normalizedSource, attachment))
            {
                matched = attachment;
            }
        }

        if (matched is not null)
        {
            return NormalizeRelativePath(matched.RelativePath);
        }

        return noteImageCount == 1 && onlyImage is not null
            ? NormalizeRelativePath(onlyImage.RelativePath)
            : null;
    }
}
