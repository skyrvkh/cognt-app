using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace CogntApp.ViewModels;

internal static class GraphSearchResultBuilder
{
    private const int MaximumResultCount = 12;

    internal static IReadOnlyList<KnowledgeGraphNodeReadModel> CreateOrderedNodes(KnowledgeGraphReadModel graph)
    {
        if (graph.Nodes.Count == 0)
        {
            return Array.Empty<KnowledgeGraphNodeReadModel>();
        }

        List<KnowledgeGraphNodeReadModel> orderedNodes = [.. graph.Nodes];
        orderedNodes.Sort(CompareGraphSearchNodes);
        return orderedNodes;
    }

    internal static GraphSearchResult Build(
        IReadOnlyList<KnowledgeGraphNodeReadModel> orderedNodes,
        string query,
        string? selectedDocumentId)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new GraphSearchResult(Array.Empty<KnowledgeGraphNodeReadModel>(), ContainsSelectedResult: false);
        }

        bool containsSelectedResult = false;
        List<KnowledgeGraphNodeReadModel> matches = [];

        foreach (KnowledgeGraphNodeReadModel node in orderedNodes)
        {
            if (!node.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !node.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches.Add(node);
            if (!containsSelectedResult
                && selectedDocumentId is not null
                && string.Equals(node.DocumentId, selectedDocumentId, StringComparison.OrdinalIgnoreCase))
            {
                containsSelectedResult = true;
            }

            if (matches.Count >= MaximumResultCount)
            {
                break;
            }
        }

        return new GraphSearchResult(
            matches.Count == 0 ? Array.Empty<KnowledgeGraphNodeReadModel>() : [.. matches],
            containsSelectedResult);
    }

    private static int CompareGraphSearchNodes(
        KnowledgeGraphNodeReadModel left,
        KnowledgeGraphNodeReadModel right)
    {
        int depthComparison = left.Depth.CompareTo(right.Depth);
        if (depthComparison != 0)
        {
            return depthComparison;
        }

        int titleComparison = StringComparer.OrdinalIgnoreCase.Compare(left.Title, right.Title);
        return titleComparison != 0
            ? titleComparison
            : StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath);
    }
}

internal readonly record struct GraphSearchResult(
    IReadOnlyList<KnowledgeGraphNodeReadModel> Nodes,
    bool ContainsSelectedResult);
