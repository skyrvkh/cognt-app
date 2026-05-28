using System.Text;
using System.Xml.Linq;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class HtmlBlockSuiteDocumentConverter
{
    private static void AppendTextBlock(
        List<BlockSuiteDocumentBlock> blocks,
        string text,
        string blockType = "text")
    {
        BlockSuiteDocumentBlockBuilder.AppendNormalizedTextBlock(blocks, text, blockType);
    }

    private static string ReadInlineText(XElement element)
    {
        return BlockSuiteDocumentBlockBuilder.NormalizeText(ReadText(element));
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

    private static string NormalizeHtmlEntities(string html)
    {
        return html
            .Replace("&nbsp;", "&#160;", StringComparison.OrdinalIgnoreCase)
            .Replace("&emsp;", "&#8195;", StringComparison.OrdinalIgnoreCase)
            .Replace("&ensp;", "&#8194;", StringComparison.OrdinalIgnoreCase);
    }
}
