using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Navigation = CogntApp.ViewModels.Navigation;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void RefreshEditorBreadcrumb()
    {
        EditorBreadcrumbItems.Clear();

        if (!currentNoteItemId.HasValue || string.IsNullOrWhiteSpace(openCollectionPath))
        {
            return;
        }

        IReadOnlyList<KnowledgeTreeItemReadModel> fullChain = LoadBreadcrumbChain(currentNoteItemId.Value);
        int visibleStartIndex = fullChain.Count > 3 ? fullChain.Count - 3 : 0;
        int visibleCount = fullChain.Count - visibleStartIndex;

        for (int index = 0; index < visibleCount; index++)
        {
            KnowledgeTreeItemReadModel item = fullChain[visibleStartIndex + index];
            bool isNote = item.Kind == KnowledgeItemKind.Note;
            EditorBreadcrumbItems.Add(new Navigation.EditorBreadcrumbItemViewModel(
                item.Id,
                item.Title,
                ResolveNotePath(item),
                isNote,
                item.Id == currentNoteItemId,
                index == visibleCount - 1));
        }
    }

    private IReadOnlyList<KnowledgeTreeItemReadModel> LoadBreadcrumbChain(long itemId)
    {
        try
        {
            return noteService.LoadBreadcrumbChain(openCollectionPath, itemId);
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            return [];
        }
    }
}
