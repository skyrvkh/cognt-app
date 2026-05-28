using Mammoth;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static class DocxBlockSuiteDocumentConverter
{
    internal static string Convert(Stream docxContent, string title, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(docxContent);
        string normalizedTitle = title ?? string.Empty;

        if (docxContent.CanSeek)
        {
            docxContent.Position = 0;
        }

        try
        {
            DocumentConverter converter = new();
            IResult<string> result = converter.ConvertToHtml(docxContent);
            IReadOnlyList<object> warnings = result.Warnings.Cast<object>().ToArray();
            if (warnings.Count > 0)
            {
                logger?.LogWarning(
                    "DOCX to HTML conversion completed with warnings. WarningCount={WarningCount}",
                    warnings.Count);
                logger?.LogDebug(
                    "DOCX to HTML conversion warnings. WarningCount={WarningCount} Warnings={Warnings}",
                    warnings.Count,
                    string.Join(" | ", warnings));
            }


            return HtmlBlockSuiteDocumentConverter.Convert(result.Value ?? string.Empty, normalizedTitle, logger);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(
                ex,
                "DOCX to BlockSuite conversion failed. TitleLength={TitleLength} StreamCanSeek={StreamCanSeek}",
                normalizedTitle.Length,
                docxContent.CanSeek);
            throw;
        }
    }
}
