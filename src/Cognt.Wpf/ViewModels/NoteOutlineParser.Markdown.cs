using System.Text.RegularExpressions;

namespace CogntApp.ViewModels;

internal static partial class NoteOutlineParser
{
    private static readonly Regex MarkdownHeadingRegex = new(@"^\s{0,3}(#{1,6})\s+(.+?)\s*#*\s*$", RegexOptions.Compiled);

    private static IReadOnlyList<NoteOutlineItemViewModel> ParseMarkdownOutline(string content)
    {
        List<NoteOutlineItemViewModel> items = [];
        int generatedId = 0;
        foreach (string line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            Match match = MarkdownHeadingRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            int level = Math.Clamp(match.Groups[1].Value.Length, 1, 6);
            string title = StripInlineMarkdown(match.Groups[2].Value);
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            generatedId++;
            items.Add(new NoteOutlineItemViewModel(title, level, $"markdown-heading-{generatedId}"));
        }

        return items;
    }

    private static string StripInlineMarkdown(string text)
    {
        return text
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("*", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim();
    }
}
