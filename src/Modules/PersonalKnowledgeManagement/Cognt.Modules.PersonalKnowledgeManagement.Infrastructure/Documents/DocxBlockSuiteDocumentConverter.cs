using System.IO.Compression;
using System.Xml.Linq;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class DocxBlockSuiteDocumentConverter
{
    private static readonly XNamespace Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    internal static string Convert(Stream docxContent, string title)
    {
        ArgumentNullException.ThrowIfNull(docxContent);

        if (docxContent.CanSeek)
        {
            docxContent.Position = 0;
        }

        using ZipArchive archive = new(docxContent, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry documentEntry = archive.GetEntry("word/document.xml")
            ?? throw new InvalidOperationException("The DOCX file does not contain a Word document body.");

        using Stream documentStream = documentEntry.Open();
        XDocument document = XDocument.Load(documentStream);
        XElement body = document.Descendants(Word + "body").FirstOrDefault()
            ?? throw new InvalidOperationException("The DOCX document body could not be read.");

        List<BlockSuiteDocumentBlock> blocks = [];
        foreach (XElement child in body.Elements())
        {
            if (child.Name == Word + "p")
            {
                AppendParagraphBlock(blocks, child);
            }
            else if (child.Name == Word + "tbl")
            {
                AppendTableBlock(blocks, child);
            }
        }

        return BlockSuiteDocumentContent.FromBlocks(blocks, title);
    }
}
