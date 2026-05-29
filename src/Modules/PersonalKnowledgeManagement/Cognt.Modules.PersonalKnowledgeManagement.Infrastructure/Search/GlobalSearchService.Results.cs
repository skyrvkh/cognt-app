using Cognt.Modules.PersonalKnowledgeManagement.Application.Search;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Search;

internal sealed partial class GlobalSearchService
{
    private ScoredSearchResult CreateRankedMatch(
        GlobalSearchCollectionScope collection,
        KnowledgeNoteListItemReadModel candidate,
        string bodyText,
        string query,
        IReadOnlyList<string> queryTerms,
        double score,
        DateTimeOffset lastModifiedUtc,
        int sequenceIndex)
    {
        string snippet = CreateSnippet(bodyText, candidate.Title, query, queryTerms);
        return new ScoredSearchResult(
            new GlobalSearchResultReadModel(
                candidate.Title,
                snippet,
                candidate.RelativePath,
                collection.CollectionId,
                collection.CollectionName),
            score,
            lastModifiedUtc,
            sequenceIndex);
    }

    private static IReadOnlyList<GlobalSearchResultReadModel> OrderSearchResults(
        IReadOnlyList<ScoredSearchResult> rankedMatches,
        int limit)
    {
        if (rankedMatches.Count == 0)
        {
            return [];
        }

        ScoredSearchResult[] orderedMatches = new ScoredSearchResult[rankedMatches.Count];
        for (int index = 0; index < orderedMatches.Length; index++)
        {
            orderedMatches[index] = rankedMatches[index];
        }

        Array.Sort(orderedMatches, CompareSearchResults);

        int resultCount = Math.Min(limit, orderedMatches.Length);
        GlobalSearchResultReadModel[] results = new GlobalSearchResultReadModel[resultCount];
        for (int index = 0; index < results.Length; index++)
        {
            results[index] = orderedMatches[index].Result;
        }

        return results;
    }

    private static int CompareSearchResults(ScoredSearchResult left, ScoredSearchResult right)
    {
        int scoreComparison = right.Score.CompareTo(left.Score);
        if (scoreComparison != 0)
        {
            return scoreComparison;
        }

        DateTimeOffset leftLastModified = left.LastModifiedUtc ?? DateTimeOffset.MinValue;
        DateTimeOffset rightLastModified = right.LastModifiedUtc ?? DateTimeOffset.MinValue;
        int modifiedComparison = rightLastModified.CompareTo(leftLastModified);
        if (modifiedComparison != 0)
        {
            return modifiedComparison;
        }

        int titleComparison = string.Compare(left.Result.Title, right.Result.Title, StringComparison.OrdinalIgnoreCase);
        return titleComparison != 0
            ? titleComparison
            : left.SequenceIndex.CompareTo(right.SequenceIndex);
    }

    private sealed class ScoredSearchResult
    {
        internal ScoredSearchResult(
            GlobalSearchResultReadModel result,
            double score,
            DateTimeOffset? lastModifiedUtc,
            int sequenceIndex)
        {
            Result = result;
            Score = score;
            LastModifiedUtc = lastModifiedUtc;
            SequenceIndex = sequenceIndex;
        }

        internal GlobalSearchResultReadModel Result { get; }

        internal double Score { get; }

        internal DateTimeOffset? LastModifiedUtc { get; }

        internal int SequenceIndex { get; }
    }
}
