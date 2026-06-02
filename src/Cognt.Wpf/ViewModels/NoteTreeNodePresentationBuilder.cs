using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace CogntApp.ViewModels;

internal sealed record NoteTreeNodePresentation(
    NoteTreeNodeViewModel Node,
    bool ShouldLoadFlashcardChildrenImmediately);

internal static class NoteTreeNodePresentationBuilder
{
    internal static NoteTreeNodePresentation CreateTreeNode(
        KnowledgeTreeItemReadModel item,
        string? notePath)
    {
        NoteTreeNodeViewModel node = new(
            item.Id,
            item.ParentId,
            item.Kind,
            item.Title,
            item.FileRelPath,
            notePath,
            item.ChildCount)
        {
            IsExpanded = false,
        };

        bool shouldDeferChildren = item.ChildCount > 0 && node.CanContainChildren;
        if (shouldDeferChildren)
        {
            node.Children.Add(NoteTreeNodeViewModel.CreatePlaceholder());
        }

        return new(
            node,
            ShouldLoadFlashcardChildrenImmediately: !shouldDeferChildren);
    }

    internal static NoteTreeNodeViewModel CreateFlashcardNode(
        FlashcardCardSummaryReadModel card,
        NoteTreeNodeViewModel parent)
    {
        return NoteTreeNodeViewModel.CreateFlashcardNode(
            card.Id,
            parent.Id,
            CreateFlashcardTreeTitle(card),
            parent.FileRelPath);
    }

    private static string CreateFlashcardTreeTitle(FlashcardCardSummaryReadModel card)
    {
        string title = string.IsNullOrWhiteSpace(card.BackText) ? card.FrontText : card.BackText;
        title = title.ReplaceLineEndings(" ").Trim();
        if (title.Length == 0)
        {
            title = $"Card {card.Id}";
        }

        const int maxLength = 72;
        return title.Length <= maxLength
            ? title
            : title[..(maxLength - 1)] + "...";
    }
}
