using System.Collections.ObjectModel;
using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Microsoft.Extensions.Logging;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void RefreshNotesTree(bool preserveExpansion = false)
    {

        HashSet<long> expandedItemIds = preserveExpansion
            ? CaptureExpandedItemIds()
            : [];

        IsLoadingCollection = true;

        try
        {
            NoteRoots.Clear();
            ResetLoadedTreeNodeIndex();

            if (string.IsNullOrWhiteSpace(openCollectionPath) || !Directory.Exists(openCollectionPath))
            {
                return;
            }

            IReadOnlyList<KnowledgeNoteListItemReadModel> noteCatalog = LoadNoteCatalog(openCollectionPath);
            PopulateRecentNotes(noteCatalog);

            foreach (KnowledgeTreeItemReadModel item in noteService.LoadRootItems(openCollectionPath))
            {
                NoteTreeNodeViewModel node = CreateTreeNode(item);
                NoteRoots.Add(node);
                IndexLoadedTreeSubtree(node);
            }

            if (expandedItemIds.Count > 0)
            {
                RestoreExpandedNodes(NoteRoots, expandedItemIds);
            }
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            logger?.LogError(ex, "Refresh notes tree failed with recoverable file-system exception. OpenCollectionPath={OpenCollectionPath}", openCollectionPath);
            NoteRoots.Clear();
            ResetLoadedTreeNodeIndex();
            RecentNotes.Clear();
            RaiseMessage("Collection Load Failed", $"The collection could not be read: {ex.Message}");
        }
        finally
        {
            IsLoadingCollection = false;
            OnPropertyChanged(nameof(ShouldShowNoNotesState));

        }
    }

    private IReadOnlyList<KnowledgeNoteListItemReadModel> LoadNoteCatalog(string collectionPath)
    {
        return noteService.LoadNoteCatalog(collectionPath);
    }

    public void LoadChildren(NoteTreeNodeViewModel node)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath)
            || node.IsPlaceholder
            || !node.CanContainChildren
            || node.AreChildrenLoaded)
        {
            return;
        }

        foreach (NoteTreeNodeViewModel child in node.Children)
        {
            UnindexLoadedTreeSubtree(child);
        }

        node.Children.Clear();

        foreach (KnowledgeTreeItemReadModel child in noteService.LoadChildren(openCollectionPath, node.Id))
        {
            NoteTreeNodeViewModel childNode = CreateTreeNode(child);
            node.Children.Add(childNode);
            IndexLoadedTreeSubtree(childNode);
        }

        AddFlashcardChildNodes(node);
        node.MarkChildrenLoaded();
    }

    private NoteTreeNodeViewModel CreateTreeNode(KnowledgeTreeItemReadModel item)
    {
        NoteTreeNodePresentation presentation =
            NoteTreeNodePresentationBuilder.CreateTreeNode(item, ResolveNotePath(item));

        if (presentation.ShouldLoadFlashcardChildrenImmediately)
        {
            AddFlashcardChildNodes(presentation.Node);
            if (presentation.Node.Children.Count > 0)
            {
                presentation.Node.MarkChildrenLoaded();
            }
        }

        return presentation.Node;
    }

    private void AddFlashcardChildNodes(NoteTreeNodeViewModel node)
    {
        if (!ShowFlashcardsInNavigationTree
            || node.IsPlaceholder
            || node.Kind != KnowledgeItemKind.Note
            || string.IsNullOrWhiteSpace(node.FileRelPath))
        {
            return;
        }

        IReadOnlyList<FlashcardCardSummaryReadModel> cards = flashcardCardsQuery.Execute(
            new FlashcardCardsQueryRequest(
                openCollectionPath,
                SourceRelPath: node.FileRelPath,
                Limit: 1_000));

        foreach (FlashcardCardSummaryReadModel card in cards)
        {
            NoteTreeNodeViewModel cardNode = NoteTreeNodePresentationBuilder.CreateFlashcardNode(card, node);
            node.Children.Add(cardNode);
            IndexLoadedTreeSubtree(cardNode);
        }
    }

    private HashSet<long> CaptureExpandedItemIds()
    {
        HashSet<long>? expandedItemIds = null;
        foreach (NoteTreeNodeViewModel node in loadedTreeNodesById.Values)
        {
            if (!node.IsExpanded)
            {
                continue;
            }

            expandedItemIds ??= new HashSet<long>(Math.Min(loadedTreeNodesById.Count, 64));
            expandedItemIds.Add(node.Id);
        }

        return expandedItemIds ?? [];
    }

    private void RestoreExpandedNodes(
        IEnumerable<NoteTreeNodeViewModel> nodes,
        IReadOnlySet<long> expandedItemIds)
    {
        foreach (NoteTreeNodeViewModel node in nodes)
        {
            if (node.IsPlaceholder || !expandedItemIds.Contains(node.Id))
            {
                continue;
            }

            LoadChildren(node);
            node.IsExpanded = true;
            RestoreExpandedNodes(node.Children, expandedItemIds);
        }
    }

}
