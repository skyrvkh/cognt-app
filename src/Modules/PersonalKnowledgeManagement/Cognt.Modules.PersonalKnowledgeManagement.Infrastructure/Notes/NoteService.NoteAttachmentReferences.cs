using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private async Task<NoteAttachmentRow> CreateNoteAttachmentReferenceAsync(
        string collectionId,
        string noteRelativePath,
        string sourceFileName,
        Stream sourceContent,
        CancellationToken cancellationToken)
    {
        ImportedAttachmentContext importedAttachment = await ImportAttachmentIntoCollectionAsync(
            collectionId,
            sourceFileName,
            sourceContent,
            cancellationToken);
        KnowledgeTreeItemRow note = LoadTargetNoteForAttachment(importedAttachment, noteRelativePath);
        NoteAttachmentRow noteAttachmentReference = BuildNoteAttachmentReference(importedAttachment, note);
        importedAttachment.Repository.UpsertNoteAttachment(noteAttachmentReference);

        return noteAttachmentReference;
    }

    private KnowledgeTreeItemRow LoadTargetNoteForAttachment(
        ImportedAttachmentContext importedAttachment,
        string noteRelativePath)
    {
        string fullNotePath = ResolveExistingRelativeNotePathInRoot(importedAttachment.Root, noteRelativePath);
        string noteRelPath = NormalizeRelativePath(Path.GetRelativePath(importedAttachment.Root, fullNotePath));

        return importedAttachment.Repository.FindNoteByFileRelPath(noteRelPath)
            ?? throw new InvalidOperationException("The current note could not be found in this collection.");
    }

    private static NoteAttachmentRow BuildNoteAttachmentReference(
        ImportedAttachmentContext importedAttachment,
        KnowledgeTreeItemRow note)
    {
        long createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new NoteAttachmentRow(
            CreateNoteAttachmentId(note.Uid, importedAttachment.Attachment.AttachmentId),
            note.Id,
            note.Uid,
            importedAttachment.Attachment.AttachmentId,
            importedAttachment.Attachment.OriginalFileName,
            createdAt);
    }
}
