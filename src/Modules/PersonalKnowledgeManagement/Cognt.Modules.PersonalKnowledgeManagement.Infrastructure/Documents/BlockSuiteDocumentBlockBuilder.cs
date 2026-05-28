using System.Text;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class BlockSuiteDocumentBlockBuilder
{
    internal static void AppendParagraphIfPresent(
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

    internal static void AppendNormalizedTextBlock(
        List<BlockSuiteDocumentBlock> blocks,
        string text,
        string blockType = "text")
    {
        string normalizedText = NormalizeText(text);
        if (!string.IsNullOrWhiteSpace(normalizedText))
        {
            blocks.Add(new BlockSuiteDocumentBlock(blockType, normalizedText));
        }
    }

    internal static void AppendCodeBlock(
        List<BlockSuiteDocumentBlock> blocks,
        string text,
        string? language = null)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            blocks.Add(new BlockSuiteDocumentBlock("code", text, Language: language));
        }
    }
}
