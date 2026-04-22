using System.Text;
using System.Xml.Linq;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class DocxBlockSuiteDocumentConverter
{
    private static void AppendParagraphBlock(List<BlockSuiteDocumentBlock> blocks, XElement paragraph)
    {
        string text = ExtractParagraphText(paragraph);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string type = DetermineParagraphBlockType(paragraph);
        blocks.Add(new BlockSuiteDocumentBlock(type, text.Trim()));
    }

    private static void AppendTableBlock(List<BlockSuiteDocumentBlock> blocks, XElement table)
    {
        string[] rows = ExtractTableRows(table);
        if (rows.Length == 0)
        {
            return;
        }

        blocks.Add(new BlockSuiteDocumentBlock("code", string.Join(Environment.NewLine, rows), Language: "table"));
    }

    private static string[] ExtractTableRows(XElement table)
    {
        return table
            .Elements(Word + "tr")
            .Select(row => string.Join(
                " | ",
                row.Elements(Word + "tc")
                    .Select(cell => string.Join(" ", cell.Elements(Word + "p").Select(ExtractParagraphText)).Trim())
                    .Where(text => !string.IsNullOrWhiteSpace(text))))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
    }

    private static string DetermineParagraphBlockType(XElement paragraph)
    {
        string? style = paragraph
            .Element(Word + "pPr")
            ?.Element(Word + "pStyle")
            ?.Attribute(Word + "val")
            ?.Value;

        if (!string.IsNullOrWhiteSpace(style)
            && style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(new string(style.Where(char.IsDigit).ToArray()), out int headingLevel))
        {
            return string.Concat("h", Math.Clamp(headingLevel, 1, 6));
        }

        XElement? numbering = paragraph
            .Element(Word + "pPr")
            ?.Element(Word + "numPr");

        return numbering is null ? "text" : "bulleted";
    }

    private static string ExtractParagraphText(XElement paragraph)
    {
        StringBuilder builder = new();
        foreach (XElement node in paragraph.Descendants())
        {
            if (node.Name == Word + "t")
            {
                builder.Append(node.Value);
            }
            else if (node.Name == Word + "tab")
            {
                builder.Append('\t');
            }
            else if (node.Name == Word + "br")
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}
