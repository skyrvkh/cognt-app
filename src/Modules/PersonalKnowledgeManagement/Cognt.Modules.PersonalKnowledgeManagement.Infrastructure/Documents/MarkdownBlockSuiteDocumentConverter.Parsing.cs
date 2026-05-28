using System.Text;
using System.Text.RegularExpressions;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class MarkdownBlockSuiteDocumentConverter
{
    private static List<BlockSuiteDocumentBlock> ParseMarkdownBlocks(string markdown)
    {
        List<BlockSuiteDocumentBlock> blocks = [];
        StringBuilder paragraph = new();
        StringBuilder code = new();
        bool inCode = false;
        string codeLanguage = string.Empty;

        foreach (string rawLine in markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string line = rawLine.TrimEnd();

            Match codeFenceMatch = CodeFenceRegex.Match(line);
            if (codeFenceMatch.Success)
            {
                if (inCode)
                {
                    BlockSuiteDocumentBlockBuilder.AppendCodeBlock(
                        blocks,
                        code.ToString().TrimEnd('\n'),
                        codeLanguage);
                    code.Clear();
                    codeLanguage = string.Empty;
                    inCode = false;
                }
                else
                {
                    BlockSuiteDocumentBlockBuilder.AppendParagraphIfPresent(blocks, paragraph);
                    codeLanguage = codeFenceMatch.Groups["language"].Value;
                    inCode = true;
                }

                continue;
            }

            if (inCode)
            {
                code.AppendLine(rawLine);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                BlockSuiteDocumentBlockBuilder.AppendParagraphIfPresent(blocks, paragraph);
                continue;
            }

            Match headingMatch = HeadingRegex.Match(line);
            if (TryAppendHeadingBlock(blocks, paragraph, headingMatch))
            {
                continue;
            }

            if (TryAppendQuoteBlock(blocks, paragraph, line))
            {
                continue;
            }

            Match checklistMatch = ChecklistRegex.Match(line);
            if (TryAppendChecklistBlock(blocks, paragraph, checklistMatch))
            {
                continue;
            }

            Match orderedMatch = OrderedListRegex.Match(line);
            if (TryAppendOrderedListBlock(blocks, paragraph, orderedMatch))
            {
                continue;
            }

            Match unorderedMatch = UnorderedListRegex.Match(line);
            if (TryAppendUnorderedListBlock(blocks, paragraph, unorderedMatch))
            {
                continue;
            }

            if (paragraph.Length > 0)
            {
                paragraph.Append(' ');
            }

            paragraph.Append(RemoveInlineMarkdownMarkers(line.Trim()));
        }

        if (inCode)
        {
            BlockSuiteDocumentBlockBuilder.AppendCodeBlock(
                blocks,
                code.ToString().TrimEnd('\n'),
                codeLanguage);
        }

        BlockSuiteDocumentBlockBuilder.AppendParagraphIfPresent(blocks, paragraph);
        return blocks;
    }
}
