using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class MarkdownBlockSuiteDocumentConverter
{
    private static readonly Regex HeadingRegex = new(@"^\s{0,3}(#{1,6})\s+(.+?)\s*#*\s*$", RegexOptions.Compiled);
    private static readonly Regex ChecklistRegex = new(@"^\s*[-*+]\s+\[(?<checked>[ xX])\]\s+(?<text>.+)$", RegexOptions.Compiled);
    private static readonly Regex UnorderedListRegex = new(@"^\s*[-*+]\s+(?<text>.+)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^\s*\d+[\.)]\s+(?<text>.+)$", RegexOptions.Compiled);
    private static readonly Regex CodeFenceRegex = new(@"^\s*`{3,}(?<language>[\w#+.-]*)\s*$", RegexOptions.Compiled);

    internal static string Convert(string markdown, string title, ILogger? logger = null)
    {
        string normalizedMarkdown = markdown ?? string.Empty;
        string normalizedTitle = title ?? string.Empty;
        int lineCount = CountLines(normalizedMarkdown);

        try
        {
            List<BlockSuiteDocumentBlock> blocks = ParseMarkdownBlocks(normalizedMarkdown);
            string content = BlockSuiteDocumentContent.FromBlocks(blocks, normalizedTitle);

            return content;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(
                ex,
                "Markdown to BlockSuite conversion failed. TitleLength={TitleLength} MarkdownLength={MarkdownLength} LineCount={LineCount}",
                normalizedTitle.Length,
                normalizedMarkdown.Length,
                lineCount);
            throw;
        }
    }

    private static int CountLines(string value)
    {
        if (value.Length == 0)
        {
            return 0;
        }

        int count = 1;
        foreach (char character in value)
        {
            if (character == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static string CreateBlockTypeSummary(IReadOnlyList<BlockSuiteDocumentBlock> blocks)
    {
        return string.Join(
            ",",
            blocks
                .GroupBy(static block => block.Type, StringComparer.OrdinalIgnoreCase)
                .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(static group => $"{group.Key}:{group.Count()}"));
    }
}
