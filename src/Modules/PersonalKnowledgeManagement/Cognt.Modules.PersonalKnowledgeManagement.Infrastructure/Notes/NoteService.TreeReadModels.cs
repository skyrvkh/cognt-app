using Cognt.Modules.PersonalKnowledgeManagement.Application.Attachments;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static KnowledgeTreeItemReadModel? ToTreeItemReadModelOrNull(
        KnowledgeTreeItemRow? row)
    {
        return row is null
            ? null
            : ToTreeItemReadModel(row);
    }

    private static KnowledgeTreeItemReadModel ToTreeItemReadModel(KnowledgeTreeItemRow row)
    {
        return new KnowledgeTreeItemReadModel(
            row.Id,
            row.ParentId,
            ToKnowledgeItemKind(row.Kind),
            row.Title,
            row.FileRelPath,
            row.ChildCount);
    }

    private static KnowledgeItemKind ToKnowledgeItemKind(KnowledgeTreeItemKind kind)
    {
        return kind switch
        {
            KnowledgeTreeItemKind.Folder => KnowledgeItemKind.Folder,
            KnowledgeTreeItemKind.Note => KnowledgeItemKind.Note,
            KnowledgeTreeItemKind.Root => KnowledgeItemKind.Root,
            KnowledgeTreeItemKind.Trash => KnowledgeItemKind.Trash,
            KnowledgeTreeItemKind.PdfDocument => KnowledgeItemKind.PdfDocument,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown knowledge tree item kind.")
        };
    }

    private static KnowledgeNoteListItemType ClassifyNoteListItemType(
        string notePath,
        IReadOnlyList<AttachmentRow> attachments)
    {
        if (attachments.Any(attachment => InferAttachmentKind(attachment.FileExtension, attachment.ContentType) == KnowledgeAttachmentKind.Pdf))
        {
            return KnowledgeNoteListItemType.PdfLinkedNote;
        }

        if (attachments.Count > 0)
        {
            return KnowledgeNoteListItemType.AttachmentBackedNote;
        }

        string extension = Path.GetExtension(notePath);
        KnowledgeNoteListItemType itemType = string.Equals(extension, MarkdownNoteExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, MarkdownImportExtension, StringComparison.OrdinalIgnoreCase)
                ? KnowledgeNoteListItemType.Markdown
                : KnowledgeNoteListItemType.Note;
        return itemType;
    }
}
