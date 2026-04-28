using System.Text.RegularExpressions;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static IEnumerable<DocumentLinkTitleMatch> EnumerateTitleMentionMatches(
        string plainText,
        DocumentLinkNote note)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            yield break;
        }

        Regex regex = new(
            $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(note.Title)}(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        foreach (Match match in regex.Matches(plainText))
        {
            yield return new DocumentLinkTitleMatch(
                match.Index,
                match.Length,
                match.Value);
        }
    }

    private sealed class DocumentLinkTitleMatch
    {
        internal DocumentLinkTitleMatch(
            int matchStart,
            int matchLength,
            string matchedText)
        {
            MatchStart = matchStart;
            MatchLength = matchLength;
            MatchedText = matchedText;
        }

        internal int MatchStart { get; }

        internal int MatchLength { get; }

        internal string MatchedText { get; }
    }
}
