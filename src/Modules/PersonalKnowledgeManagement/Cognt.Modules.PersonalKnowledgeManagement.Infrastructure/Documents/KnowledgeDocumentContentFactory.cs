using Cognt.Modules.PersonalKnowledgeManagement.Application.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal sealed class KnowledgeDocumentContentFactory : IKnowledgeDocumentContentFactory
{
    string IKnowledgeDocumentContentFactory.FromPlainText(string text, string title)
    {
        return BlockSuiteDocumentContent.FromPlainText(text, title);
    }

    string IKnowledgeDocumentContentFactory.FromMarkdown(string markdown, string title)
    {
        return MarkdownBlockSuiteDocumentConverter.Convert(markdown, title);
    }

    string IKnowledgeDocumentContentFactory.FromDocx(Stream docxContent, string title)
    {
        return DocxBlockSuiteDocumentConverter.Convert(docxContent, title);
    }
}
