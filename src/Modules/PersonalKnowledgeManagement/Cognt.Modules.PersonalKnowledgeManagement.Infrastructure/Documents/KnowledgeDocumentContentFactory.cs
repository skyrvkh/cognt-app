using Cognt.Modules.PersonalKnowledgeManagement.Application.Documents;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal sealed class KnowledgeDocumentContentFactory(ILogger<KnowledgeDocumentContentFactory>? logger = null) : IKnowledgeDocumentContentFactory
{
    string IKnowledgeDocumentContentFactory.FromPlainText(string text, string title)
    {
        string normalizedText = text ?? string.Empty;
        string normalizedTitle = title ?? string.Empty;
        return BlockSuiteDocumentContent.FromPlainText(normalizedText, normalizedTitle);
    }

    string IKnowledgeDocumentContentFactory.FromMarkdown(string markdown, string title)
    {
        string normalizedMarkdown = markdown ?? string.Empty;
        string normalizedTitle = title ?? string.Empty;
        return MarkdownBlockSuiteDocumentConverter.Convert(normalizedMarkdown, normalizedTitle, logger);
    }

    string IKnowledgeDocumentContentFactory.FromDocx(Stream docxContent, string title)
    {
        ArgumentNullException.ThrowIfNull(docxContent);
        string normalizedTitle = title ?? string.Empty;
        return DocxBlockSuiteDocumentConverter.Convert(docxContent, normalizedTitle, logger);
    }
}
