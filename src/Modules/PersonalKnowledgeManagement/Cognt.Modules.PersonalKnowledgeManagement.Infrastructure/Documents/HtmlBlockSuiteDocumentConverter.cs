using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class HtmlBlockSuiteDocumentConverter
{
    internal static string Convert(string html, string title, ILogger? logger = null)
    {
        string normalizedHtml = html ?? string.Empty;
        string normalizedTitle = title ?? string.Empty;

        try
        {
            List<BlockSuiteDocumentBlock> blocks = ParseBlocks(normalizedHtml);
            string content = BlockSuiteDocumentContent.FromBlocks(blocks, normalizedTitle);

            return content;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(
                ex,
                "HTML to BlockSuite conversion failed. TitleLength={TitleLength} HtmlLength={HtmlLength}",
                normalizedTitle.Length,
                normalizedHtml.Length);
            throw;
        }
    }

    private static List<BlockSuiteDocumentBlock> ParseBlocks(string html)
    {
        XDocument document = XDocument.Parse("<root>" + NormalizeHtmlEntities(html) + "</root>");
        List<BlockSuiteDocumentBlock> blocks = [];
        foreach (XNode node in document.Root?.Nodes() ?? [])
        {
            AppendBlock(blocks, node);
        }

        return blocks;
    }

    private static void AppendBlock(List<BlockSuiteDocumentBlock> blocks, XNode node)
    {
        if (node is XText text)
        {
            AppendTextBlock(blocks, text.Value);
            return;
        }

        if (node is not XElement element)
        {
            return;
        }

        string name = element.Name.LocalName.ToLowerInvariant();
        switch (name)
        {
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                AppendTextBlock(blocks, ReadInlineText(element), name);
                return;
            case "p":
                AppendTextBlock(blocks, ReadInlineText(element));
                return;
            case "blockquote":
                AppendTextBlock(blocks, ReadInlineText(element), "quote");
                return;
            case "pre":
                AppendCodeBlock(blocks, element);
                return;
            case "ul":
                AppendListBlocks(blocks, element, "bulleted");
                return;
            case "ol":
                AppendListBlocks(blocks, element, "numbered");
                return;
            case "li":
                AppendTextBlock(blocks, ReadInlineText(element), "bulleted");
                return;
            case "table":
                AppendTableBlock(blocks, element);
                return;
            case "br":
                return;
            default:
                foreach (XNode child in element.Nodes())
                {
                    AppendBlock(blocks, child);
                }

                return;
        }
    }

    private static void AppendListBlocks(
        List<BlockSuiteDocumentBlock> blocks,
        XElement list,
        string blockType)
    {
        foreach (XElement item in list.Elements())
        {
            if (string.Equals(item.Name.LocalName, "li", StringComparison.OrdinalIgnoreCase))
            {
                AppendTextBlock(blocks, ReadInlineText(item), blockType);
            }
        }
    }

    private static void AppendCodeBlock(List<BlockSuiteDocumentBlock> blocks, XElement element)
    {
        string text = ReadText(element).Trim('\r', '\n');
        if (!string.IsNullOrWhiteSpace(text))
        {
            BlockSuiteDocumentBlockBuilder.AppendCodeBlock(blocks, text);
        }
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
