using System.Text.RegularExpressions;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class MarkdownBlockSuiteDocumentConverter
{
    private static readonly Regex HeadingRegex = new(@"^\s{0,3}(#{1,6})\s+(.+?)\s*#*\s*$", RegexOptions.Compiled);
    private static readonly Regex ChecklistRegex = new(@"^\s*[-*+]\s+\[(?<checked>[ xX])\]\s+(?<text>.+)$", RegexOptions.Compiled);
    private static readonly Regex UnorderedListRegex = new(@"^\s*[-*+]\s+(?<text>.+)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^\s*\d+[\.)]\s+(?<text>.+)$", RegexOptions.Compiled);
    private static readonly Regex CodeFenceRegex = new(@"^\s*`{3,}(?<language>[\w#+.-]*)\s*$", RegexOptions.Compiled);

    internal static string Convert(string markdown, string title)
    {
        string normalizedMarkdown = markdown ?? string.Empty;
        List<BlockSuiteDocumentBlock> blocks = ParseMarkdownBlocks(normalizedMarkdown);
        return BlockSuiteDocumentContent.FromBlocks(blocks, title);
    }
}
