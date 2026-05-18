using System.IO;
using System.Text.RegularExpressions;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static string ExtractTitle(string markdown, string fileName)
    {
        return TryExtractHeadingTitle(markdown) ?? Path.GetFileNameWithoutExtension(fileName);
    }

    private static string? TryExtractHeadingTitle(string markdown)
    {
        foreach (string line in markdown.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            Match match = HeadingRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            string title = SearchableDocumentText.NormalizeWhitespace(match.Groups[1].Value);
            if (title.Length > 0)
            {
                return title;
            }
        }

        return null;
    }

    private static string CreateExcerpt(string markdown, int maxCharacters)
    {
        string normalized = markdown.Trim();
        if (maxCharacters == 0 || normalized.Length <= maxCharacters)
        {
            return normalized;
        }

        string candidate = normalized[..maxCharacters];
        int lastLineBreak = candidate.LastIndexOf('\n');
        if (lastLineBreak >= 200)
        {
            candidate = candidate[..lastLineBreak];
        }

        return $"{candidate.TrimEnd()}...";
    }
}
