using System.Text;
using System.Text.RegularExpressions;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class MarkdownBlockSuiteDocumentConverter
{
    private static bool TryAppendHeadingBlock(
        List<BlockSuiteDocumentBlock> blocks,
        StringBuilder paragraph,
        Match headingMatch)
    {
        if (!headingMatch.Success)
        {
            return false;
        }

        BlockSuiteDocumentBlockBuilder.AppendParagraphIfPresent(blocks, paragraph);
        string type = string.Concat("h", Math.Min(6, headingMatch.Groups[1].Value.Length));
        blocks.Add(new BlockSuiteDocumentBlock(type, RemoveInlineMarkdownMarkers(headingMatch.Groups[2].Value)));
        return true;
    }

    private static bool TryAppendQuoteBlock(
        List<BlockSuiteDocumentBlock> blocks,
        StringBuilder paragraph,
        string line)
    {
        if (!line.TrimStart().StartsWith('>'))
        {
            return false;
        }

        BlockSuiteDocumentBlockBuilder.AppendParagraphIfPresent(blocks, paragraph);
        blocks.Add(new BlockSuiteDocumentBlock(
            "quote",
            RemoveInlineMarkdownMarkers(line.TrimStart().TrimStart('>').Trim())));
        return true;
    }

    private static bool TryAppendChecklistBlock(
        List<BlockSuiteDocumentBlock> blocks,
        StringBuilder paragraph,
        Match checklistMatch)
    {
        if (!checklistMatch.Success)
        {
            return false;
        }

        BlockSuiteDocumentBlockBuilder.AppendParagraphIfPresent(blocks, paragraph);
        bool isChecked = !string.IsNullOrWhiteSpace(checklistMatch.Groups["checked"].Value);
        blocks.Add(new BlockSuiteDocumentBlock(
            "todo",
            RemoveInlineMarkdownMarkers(checklistMatch.Groups["text"].Value),
            isChecked));
        return true;
    }

    private static bool TryAppendOrderedListBlock(
        List<BlockSuiteDocumentBlock> blocks,
        StringBuilder paragraph,
        Match orderedMatch)
    {
        if (!orderedMatch.Success)
        {
            return false;
        }

        BlockSuiteDocumentBlockBuilder.AppendParagraphIfPresent(blocks, paragraph);
        blocks.Add(new BlockSuiteDocumentBlock(
            "numbered",
            RemoveInlineMarkdownMarkers(orderedMatch.Groups["text"].Value)));
        return true;
    }

    private static bool TryAppendUnorderedListBlock(
        List<BlockSuiteDocumentBlock> blocks,
        StringBuilder paragraph,
        Match unorderedMatch)
    {
        if (!unorderedMatch.Success)
        {
            return false;
        }

        BlockSuiteDocumentBlockBuilder.AppendParagraphIfPresent(blocks, paragraph);
        blocks.Add(new BlockSuiteDocumentBlock(
            "bulleted",
            RemoveInlineMarkdownMarkers(unorderedMatch.Groups["text"].Value)));
        return true;
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
