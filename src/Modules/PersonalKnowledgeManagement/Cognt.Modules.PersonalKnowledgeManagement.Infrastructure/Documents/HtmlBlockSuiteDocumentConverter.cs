using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class HtmlBlockSuiteDocumentConverter
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    internal static string Convert(string html, string title)
    {
        List<BlockSuiteDocumentBlock> blocks = ParseBlocks(html ?? string.Empty);
        return BlockSuiteDocumentContent.FromBlocks(blocks, title);
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
            blocks.Add(new BlockSuiteDocumentBlock("code", text));
        }
    }

    private static void AppendTableBlock(List<BlockSuiteDocumentBlock> blocks, XElement table)
    {
        StringBuilder rows = new();
        foreach (XElement element in table.Descendants())
        {
            if (!string.Equals(element.Name.LocalName, "tr", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string row = ReadTableRow(element);
            if (string.IsNullOrWhiteSpace(row))
            {
                continue;
            }

            if (rows.Length > 0)
            {
                rows.AppendLine();
            }

            rows.Append(row);
        }

        if (rows.Length > 0)
        {
            blocks.Add(new BlockSuiteDocumentBlock("code", rows.ToString(), Language: "table"));
        }
    }

    private static string ReadTableRow(XElement row)
    {
        StringBuilder builder = new();
        foreach (XElement cell in row.Elements())
        {
            if (!string.Equals(cell.Name.LocalName, "td", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(cell.Name.LocalName, "th", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string text = ReadInlineText(cell);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(" | ");
            }

            builder.Append(text);
        }

        return builder.ToString();
    }

    private static void AppendTextBlock(
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

    private static string ReadInlineText(XElement element)
    {
        return NormalizeText(ReadText(element));
    }

    private static string ReadText(XElement element)
    {
        StringBuilder builder = new();
        AppendText(element, builder);
        return builder.ToString();
    }

    private static void AppendText(XNode node, StringBuilder builder)
    {
        switch (node)
        {
            case XText text:
                builder.Append(text.Value);
                return;
            case XElement { Name.LocalName: "br" }:
                builder.AppendLine();
                return;
            case XElement { Name.LocalName: "img" } image:
                string? alt = image.Attribute("alt")?.Value;
                if (!string.IsNullOrWhiteSpace(alt))
                {
                    builder.Append(alt);
                }

                return;
            case XElement element:
                foreach (XNode child in element.Nodes())
                {
                    AppendText(child, builder);
                }

                return;
        }
    }

    private static string NormalizeText(string value)
    {
        return WhitespaceRegex
            .Replace(value.Replace('\u00A0', ' '), " ")
            .Trim();
    }

    private static string NormalizeHtmlEntities(string html)
    {
        return html
            .Replace("&nbsp;", "&#160;", StringComparison.OrdinalIgnoreCase)
            .Replace("&emsp;", "&#8195;", StringComparison.OrdinalIgnoreCase)
            .Replace("&ensp;", "&#8194;", StringComparison.OrdinalIgnoreCase);
    }
}
