using Cognt.Modules.PersonalKnowledgeManagement.Application.Search;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Search;

internal sealed partial class GlobalSearchService
{
    private static ScoredSearchResult CreateRankedMatch(
        GlobalSearchCollectionScope collection,
        SearchCandidate candidate,
        string bodyText,
        string query,
        IReadOnlyList<string> queryTerms,
        double score,
        DateTimeOffset lastModifiedUtc)
    {
        string snippet = CreateSnippet(bodyText, candidate.Note.Title, query, queryTerms);
        return new ScoredSearchResult(
            new GlobalSearchResultReadModel(
                candidate.Note.Title,
                snippet,
                candidate.Note.RelativePath,
                collection.CollectionId,
                collection.CollectionName),
            score,
            lastModifiedUtc);
    }

    private static IReadOnlyList<GlobalSearchResultReadModel> OrderSearchResults(
        IReadOnlyList<ScoredSearchResult> rankedMatches,
        int limit)
    {
        return rankedMatches
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.LastModifiedUtc ?? DateTimeOffset.MinValue)
            .ThenBy(match => match.Result.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(match => match.Result)
            .ToArray();
    }

    private sealed class ScoredSearchResult
    {
        internal ScoredSearchResult(
            GlobalSearchResultReadModel result,
            double score,
            DateTimeOffset? lastModifiedUtc)
        {
            Result = result;
            Score = score;
            LastModifiedUtc = lastModifiedUtc;
        }

        internal GlobalSearchResultReadModel Result { get; }

        internal double Score { get; }

        internal DateTimeOffset? LastModifiedUtc { get; }
    }
}
