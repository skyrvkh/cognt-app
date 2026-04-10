namespace Cognt.Modules.PersonalKnowledgeManagement.Application.Documents;

public interface IKnowledgeDocumentContentFactory
{
    string FromPlainText(string text, string title);

    string FromMarkdown(string markdown, string title);

    string FromDocx(Stream docxContent, string title);
}
