using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Search;

internal sealed partial class GlobalSearchService
{
    private MatchScore ScoreMatch(
        string title,
        string bodyText,
        string query,
        IReadOnlyList<string> terms,
        DateTimeOffset lastModifiedUtc)
    {
        double score = 0;
        bool isMatch = false;
        string trimmedTitle = title.Trim();

        if (Contains(title, query))
        {
            isMatch = true;
            score += string.Equals(trimmedTitle, query, StringComparison.OrdinalIgnoreCase)
                ? ExactTitleMatchScore
                : TitlePhraseMatchScore;
            if (trimmedTitle.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                score += TitlePrefixBonusScore;
            }
        }

        if (Contains(bodyText, query))
        {
            isMatch = true;
            score += BodyPhraseMatchScore;
        }

        (int titleTermMatches, int bodyTermMatches) = CountTermMatches(title, bodyText, terms);
        if (titleTermMatches > 0 || bodyTermMatches > 0)
        {
            isMatch = true;
            score += titleTermMatches * TitleTermMatchScore;
            score += bodyTermMatches * BodyTermMatchScore;
        }

        double daysOld = Math.Max(0, (DateTimeOffset.UtcNow - lastModifiedUtc).TotalDays);
        score += Math.Max(
            0,
            MaximumRecencyBonusScore - Math.Min(MaximumRecencyBonusScore, daysOld / RecencyBonusDecayDays));
        return new MatchScore(isMatch, score);
    }

    private string CreateSnippet(
        string bodyText,
        string title,
        string query,
        IReadOnlyList<string> terms)
    {
        // TODO: Add highlighted result spans when the WPF result template supports rich text.
        string source = string.IsNullOrWhiteSpace(bodyText) ? title : bodyText;
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        int matchIndex = IndexOf(source, query);
        bool usedQueryMatch = matchIndex >= 0;
        if (matchIndex < 0)
        {
            matchIndex = FindEarliestTermMatchIndex(source, terms);
        }

        return SearchableDocumentText.CreateBoundedSnippet(source, matchIndex, query.Length, SnippetRadius);
    }

    private static (int TitleMatches, int BodyMatches) CountTermMatches(
        string title,
        string bodyText,
        IReadOnlyList<string> terms)
    {
        int titleMatches = 0;
        int bodyMatches = 0;
        foreach (string term in terms)
        {
            if (Contains(title, term))
            {
                titleMatches++;
            }

            if (Contains(bodyText, term))
            {
                bodyMatches++;
            }
        }

        return (titleMatches, bodyMatches);
    }

    private static int FindEarliestTermMatchIndex(string source, IReadOnlyList<string> terms)
    {
        int earliestIndex = -1;
        foreach (string term in terms)
        {
            int index = IndexOf(source, term);
            if (index >= 0 && (earliestIndex < 0 || index < earliestIndex))
            {
                earliestIndex = index;
            }
        }

        return earliestIndex >= 0 ? earliestIndex : 0;
    }

    private static bool Contains(string value, string query)
    {
        return IndexOf(value, query) >= 0;
    }

    private static int IndexOf(string value, string query)
    {
        return string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(query)
            ? -1
            : value.IndexOf(query, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class MatchScore
    {
        internal MatchScore(bool isMatch, double score)
        {
            IsMatch = isMatch;
            Score = score;
        }

        internal bool IsMatch { get; }

        internal double Score { get; }
    }
}
