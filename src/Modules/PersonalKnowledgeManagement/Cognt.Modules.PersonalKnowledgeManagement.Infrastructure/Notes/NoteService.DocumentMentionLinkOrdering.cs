using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal static class DocumentLinkOrdering
{
    internal static IReadOnlyList<DocumentMentionRow> ExcludeSelfTargets(
        IReadOnlyList<DocumentMentionRow> mentions,
        string currentDocumentId)
    {
        List<DocumentMentionRow> filteredMentions = new(mentions.Count);
        foreach (DocumentMentionRow mention in mentions)
        {
            if (!string.Equals(mention.TargetDocumentId, currentDocumentId, StringComparison.OrdinalIgnoreCase))
            {
                filteredMentions.Add(mention);
            }
        }

        return filteredMentions;
    }

    internal static KnowledgeDocumentLinkReadModel[] Order(
        IReadOnlyList<KnowledgeDocumentLinkReadModel> links,
        DocumentLinkSortDirection direction)
    {
        if (links.Count == 0)
        {
            return [];
        }

        DocumentLinkSortRow[] rows = new DocumentLinkSortRow[links.Count];
        for (int index = 0; index < rows.Length; index++)
        {
            rows[index] = new DocumentLinkSortRow(links[index], index);
        }

        Array.Sort(rows, (left, right) => Compare(left, right, direction));

        KnowledgeDocumentLinkReadModel[] orderedLinks = new KnowledgeDocumentLinkReadModel[rows.Length];
        for (int index = 0; index < orderedLinks.Length; index++)
        {
            orderedLinks[index] = rows[index].Link;
        }

        return orderedLinks;
    }

    private static int Compare(
        DocumentLinkSortRow left,
        DocumentLinkSortRow right,
        DocumentLinkSortDirection direction)
    {
        int comparison = direction == DocumentLinkSortDirection.Incoming
            ? (right.Link.UpdatedAt ?? 0).CompareTo(left.Link.UpdatedAt ?? 0)
            : string.Compare(left.Link.DisplayTitle, right.Link.DisplayTitle, StringComparison.OrdinalIgnoreCase);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = direction == DocumentLinkSortDirection.Incoming
            ? string.Compare(left.Link.DisplayTitle, right.Link.DisplayTitle, StringComparison.OrdinalIgnoreCase)
            : string.Compare(left.Link.OpenRelativePath, right.Link.OpenRelativePath, StringComparison.OrdinalIgnoreCase);
        return comparison != 0
            ? comparison
            : left.SequenceIndex.CompareTo(right.SequenceIndex);
    }

    private readonly struct DocumentLinkSortRow(KnowledgeDocumentLinkReadModel link, int sequenceIndex)
    {
        internal KnowledgeDocumentLinkReadModel Link { get; } = link;

        internal int SequenceIndex { get; } = sequenceIndex;
    }
}

internal enum DocumentLinkSortDirection
{
    Incoming,
    Outgoing
}
