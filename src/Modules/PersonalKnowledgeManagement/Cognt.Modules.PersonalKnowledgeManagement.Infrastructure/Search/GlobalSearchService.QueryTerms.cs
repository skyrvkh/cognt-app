namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Search;

internal sealed partial class GlobalSearchService
{
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
