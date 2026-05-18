using Mammoth;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static class DocxBlockSuiteDocumentConverter
{
    internal static string Convert(Stream docxContent, string title)
    {
        ArgumentNullException.ThrowIfNull(docxContent);

        if (docxContent.CanSeek)
        {
            docxContent.Position = 0;
        }

        DocumentConverter converter = new();
        IResult<string> result = converter.ConvertToHtml(docxContent);
        return HtmlBlockSuiteDocumentConverter.Convert(result.Value, title);
    }
}
