using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    public string? ResolveImageAttachmentRelativePath(
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

        Dictionary<string, AttachmentRow> attachmentsById = repository
            .LoadAttachments()
            .Where(IsImageAttachment)
            .ToDictionary(row => row.Id, StringComparer.OrdinalIgnoreCase);
        AttachmentRow[] noteImages = repository
            .LoadNoteAttachments()
            .Where(reference => reference.NoteItemId == note.Id)
            .Select(reference => attachmentsById.TryGetValue(reference.AttachmentId, out AttachmentRow? attachment)
                ? attachment
                : null)
            .Where(attachment => attachment is not null)
            .Cast<AttachmentRow>()
            .ToArray();

        AttachmentRow? matched = noteImages.FirstOrDefault(row => ImageSourceMatchesAttachment(normalizedSource, row));
        if (matched is not null)
        {
            return NormalizeRelativePath(matched.RelativePath);
        }

        return noteImages.Length == 1
            ? NormalizeRelativePath(noteImages[0].RelativePath)
            : null;
    }
}
