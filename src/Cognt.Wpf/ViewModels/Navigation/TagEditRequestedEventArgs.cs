using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels.Navigation;

internal sealed class TagEditRequestedEventArgs : EventArgs
{
    internal TagEditRequestedEventArgs(
        IReadOnlyList<long> itemIds,
        KnowledgeTagOperationKind operation,
        string title)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        ItemIds = CreateDistinctItemIds(itemIds);
        Operation = operation;
        Title = title;
    }

    internal IReadOnlyList<long> ItemIds { get; }

    internal KnowledgeTagOperationKind Operation { get; }

    internal string Title { get; }

    private static long[] CreateDistinctItemIds(IReadOnlyList<long> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return [];
        }

        HashSet<long> seenItemIds = [];
        List<long> distinctItemIds = new(itemIds.Count);
        foreach (long itemId in itemIds)
        {
            if (itemId > 0 && seenItemIds.Add(itemId))
            {
                distinctItemIds.Add(itemId);
            }
        }

        return distinctItemIds.ToArray();
    }
}
