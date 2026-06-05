using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    internal IReadOnlyList<string> LoadAvailableTags()
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            return [];
        }

        return noteService.LoadAvailableTags(openCollectionPath);
    }

    internal IReadOnlyList<string> LoadAssignedTags(IReadOnlyList<long> itemIds)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            return [];
        }

        return noteService.LoadAssignedTags(openCollectionPath, itemIds);
    }

    internal KnowledgeTagOperationResult ApplyTags(
        IReadOnlyList<long> itemIds,
        IReadOnlyList<string> tags,
        bool includeDescendants,
        KnowledgeTagOperationKind operation)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            throw new InvalidOperationException("Open a collection before editing tags.");
        }

        KnowledgeTagOperationResult result = operation == KnowledgeTagOperationKind.Add
            ? noteService.AssignTags(openCollectionPath, itemIds, tags, includeDescendants)
            : noteService.RemoveTags(openCollectionPath, itemIds, tags, includeDescendants);

        RefreshNotesTree(preserveExpansion: true);
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        return result;
    }
}
