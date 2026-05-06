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
        string[] queryTerms = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<ScoredSearchResult> rankedMatches = [];
        foreach (GlobalSearchCollectionScope collection in collections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(collection.CollectionId))
            {
                continue;
            }

            foreach (SearchCandidate candidate in LoadSearchCandidates(collection, query))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? content = ReadNoteContent(collection.CollectionId, candidate.Note.RelativePath);
                if (content is null)
                {
                    continue;
                }

                string bodyText = SearchableDocumentText.ExtractPlainText(candidate.Note.RelativePath, content);
                DateTimeOffset lastModifiedUtc = candidate.Note.UpdatedAt;
                MatchScore score = ScoreMatch(candidate.Note.Title, bodyText, query, queryTerms, lastModifiedUtc);
                if (!score.IsMatch)
                {
                    continue;
                }

                rankedMatches.Add(CreateRankedMatch(
                    collection,
                    candidate,
                    bodyText,
                    query,
                    queryTerms,
                    score.Score,
                    lastModifiedUtc));
            }
        }

        return OrderSearchResults(rankedMatches, limit);
    }
}
