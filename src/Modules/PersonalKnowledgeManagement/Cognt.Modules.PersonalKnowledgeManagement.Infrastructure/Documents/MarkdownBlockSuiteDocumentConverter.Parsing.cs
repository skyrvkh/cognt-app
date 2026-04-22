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
                    blocks.Add(new BlockSuiteDocumentBlock("code", code.ToString().TrimEnd('\n'), Language: codeLanguage));
                    code.Clear();
                    codeLanguage = string.Empty;
                    inCode = false;
                }
                else
                {
                    AppendParagraphBlockIfPresent(blocks, paragraph);
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
                AppendParagraphBlockIfPresent(blocks, paragraph);
                continue;
            }

            Match headingMatch = HeadingRegex.Match(line);
            if (headingMatch.Success)
            {
                AppendParagraphBlockIfPresent(blocks, paragraph);
                string type = string.Concat("h", Math.Min(6, headingMatch.Groups[1].Value.Length));
                blocks.Add(new BlockSuiteDocumentBlock(type, RemoveInlineMarkdownMarkers(headingMatch.Groups[2].Value)));
                continue;
            }

            if (line.TrimStart().StartsWith('>'))
            {
                AppendParagraphBlockIfPresent(blocks, paragraph);
                blocks.Add(new BlockSuiteDocumentBlock("quote", RemoveInlineMarkdownMarkers(line.TrimStart().TrimStart('>').Trim())));
                continue;
            }

            Match checklistMatch = ChecklistRegex.Match(line);
            if (checklistMatch.Success)
            {
                AppendParagraphBlockIfPresent(blocks, paragraph);
                bool isChecked = !string.IsNullOrWhiteSpace(checklistMatch.Groups["checked"].Value);
                blocks.Add(new BlockSuiteDocumentBlock("todo", RemoveInlineMarkdownMarkers(checklistMatch.Groups["text"].Value), isChecked));
                continue;
            }

            Match orderedMatch = OrderedListRegex.Match(line);
            if (orderedMatch.Success)
            {
                AppendParagraphBlockIfPresent(blocks, paragraph);
                blocks.Add(new BlockSuiteDocumentBlock("numbered", RemoveInlineMarkdownMarkers(orderedMatch.Groups["text"].Value)));
                continue;
            }

            Match unorderedMatch = UnorderedListRegex.Match(line);
            if (unorderedMatch.Success)
            {
                AppendParagraphBlockIfPresent(blocks, paragraph);
                blocks.Add(new BlockSuiteDocumentBlock("bulleted", RemoveInlineMarkdownMarkers(unorderedMatch.Groups["text"].Value)));
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
            blocks.Add(new BlockSuiteDocumentBlock("code", code.ToString().TrimEnd('\n'), Language: codeLanguage));
        }

        AppendParagraphBlockIfPresent(blocks, paragraph);
        return blocks;
    }

    private static void AppendParagraphBlockIfPresent(
        List<BlockSuiteDocumentBlock> blocks,
        StringBuilder paragraph)
    {
        if (paragraph.Length == 0)
        {
            return;
        }

        blocks.Add(BlockSuiteDocumentBlock.PlainText(paragraph.ToString()));
        paragraph.Clear();
    }

    private static string RemoveInlineMarkdownMarkers(string value)
    {
        return value
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("*", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal);
    }
}
