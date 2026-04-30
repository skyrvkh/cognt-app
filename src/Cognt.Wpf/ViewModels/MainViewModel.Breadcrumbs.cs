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
        IReadOnlyList<KnowledgeTreeItemReadModel> visibleChain = fullChain.Count > 3
            ? fullChain.Skip(fullChain.Count - 3).ToArray()
            : fullChain;

        for (int index = 0; index < visibleChain.Count; index++)
        {
            KnowledgeTreeItemReadModel item = visibleChain[index];
            bool isNote = item.Kind == KnowledgeItemKind.Note;
            EditorBreadcrumbItems.Add(new Navigation.EditorBreadcrumbItemViewModel(
                item.Id,
                item.Title,
                ResolveNotePath(item),
                isNote,
                item.Id == currentNoteItemId,
                index == visibleChain.Count - 1));
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
