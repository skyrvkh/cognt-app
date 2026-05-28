using System.Text;
using System.Xml.Linq;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class HtmlBlockSuiteDocumentConverter
{
    private static void AppendTableBlock(
        List<BlockSuiteDocumentBlock> blocks,
        XElement table)
    {
        StringBuilder rows = new();
        int rowCount = 0;
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
            rowCount++;
        }

        if (rows.Length > 0)
        {
            BlockSuiteDocumentBlockBuilder.AppendCodeBlock(blocks, rows.ToString(), "table");
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
}
