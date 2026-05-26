using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Search;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Search;

internal sealed partial class GlobalSearchService : IGlobalSearchService
{
    private const int DefaultLimit = 50;
    private const int MaximumLimit = 100;
    private const int SnippetRadius = 80;
    private const double ExactTitleMatchScore = 1000;
    private const double TitlePhraseMatchScore = 850;
    private const double TitlePrefixBonusScore = 100;
    private const double BodyPhraseMatchScore = 500;
    private const double TitleTermMatchScore = 80;
    private const double BodyTermMatchScore = 20;
    private const double MaximumRecencyBonusScore = 20;
    private const double RecencyBonusDecayDays = 7;

    private readonly INoteService noteService;

    internal GlobalSearchService(INoteService noteService)
    {
        ArgumentNullException.ThrowIfNull(noteService);

        this.noteService = noteService;
    }

    IReadOnlyList<GlobalSearchResultReadModel> IGlobalSearchService.Search(
        GlobalSearchRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteSearch(request, cancellationToken);
    }

    private IReadOnlyList<GlobalSearchResultReadModel> ExecuteSearch(
        GlobalSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string query = SearchableDocumentText.NormalizeWhitespace(request.Query);
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        IReadOnlyList<GlobalSearchCollectionScope> collections =
            request.Collections ?? throw new ArgumentNullException(nameof(request.Collections));
        int limit = Math.Clamp(request.Limit <= 0 ? DefaultLimit : request.Limit, 1, MaximumLimit);
        string[] queryTerms = CreateQueryTerms(query);

        List<ScoredSearchResult> rankedMatches = [];
        foreach (GlobalSearchCollectionScope collection in collections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(collection.CollectionId))
            {
                continue;
            }

            foreach (KnowledgeNoteListItemReadModel candidate in LoadSearchCandidates(collection, query))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? content = ReadNoteContent(collection.CollectionId, candidate.RelativePath);
                if (content is null)
                {
                    continue;
                }

                string bodyText = SearchableDocumentText.ExtractPlainText(candidate.RelativePath, content);
                DateTimeOffset lastModifiedUtc = candidate.UpdatedAt;
                MatchScore score = ScoreMatch(candidate.Title, bodyText, query, queryTerms, lastModifiedUtc);
                if (!score.IsMatch)
                {
                    continue;
                }

                int sequenceIndex = rankedMatches.Count;
                rankedMatches.Add(CreateRankedMatch(
                    collection,
                    candidate,
                    bodyText,
                    query,
                    queryTerms,
                    score.Score,
                    lastModifiedUtc,
                    sequenceIndex));
            }
        }

        return OrderSearchResults(rankedMatches, limit);
    }

    private static string[] CreateQueryTerms(string query)
    {
        HashSet<string> seenTerms = new(StringComparer.OrdinalIgnoreCase);
        List<string> terms = [];
        int termStart = 0;

        for (int index = 0; index <= query.Length; index++)
        {
            if (index < query.Length && query[index] != ' ')
            {
                continue;
            }

            AddQueryTerm(query, termStart, index - termStart, seenTerms, terms);
            termStart = index + 1;
        }

        return terms.ToArray();
    }

    private static void AddQueryTerm(
        string query,
        int startIndex,
        int length,
        HashSet<string> seenTerms,
        List<string> terms)
    {
        if (length <= 0)
        {
            return;
        }

        string term = query.Substring(startIndex, length);
        if (seenTerms.Add(term))
        {
            terms.Add(term);
        }
    }
}
