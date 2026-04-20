using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static KnowledgeTreeItemReadModel? ToTreeItemReadModelOrNull(KnowledgeTreeItemRow? row)
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

}
