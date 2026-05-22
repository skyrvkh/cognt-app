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

        ItemIds = itemIds.Where(id => id > 0).Distinct().ToArray();
        Operation = operation;
        Title = title;
    }

    internal IReadOnlyList<long> ItemIds { get; }

    internal KnowledgeTagOperationKind Operation { get; }

    internal string Title { get; }
}
