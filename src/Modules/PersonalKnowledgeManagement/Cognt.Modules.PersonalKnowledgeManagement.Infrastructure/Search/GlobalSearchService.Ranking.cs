using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Search;

internal sealed partial class GlobalSearchService
{
    private static MatchScore ScoreMatch(
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

        int titleTermMatches = terms.Count(term => Contains(title, term));
        int bodyTermMatches = terms.Count(term => Contains(bodyText, term));
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

    private static string CreateSnippet(
        string bodyText,
        string title,
        string query,
        IReadOnlyList<string> terms)
    {
        string source = string.IsNullOrWhiteSpace(bodyText) ? title : bodyText;
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        int matchIndex = IndexOf(source, query);
        if (matchIndex < 0)
        {
            matchIndex = terms
                .Select(term => IndexOf(source, term))
                .Where(index => index >= 0)
                .DefaultIfEmpty(0)
                .Min();
        }

        return SearchableDocumentText.CreateBoundedSnippet(source, matchIndex, query.Length, SnippetRadius);
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
