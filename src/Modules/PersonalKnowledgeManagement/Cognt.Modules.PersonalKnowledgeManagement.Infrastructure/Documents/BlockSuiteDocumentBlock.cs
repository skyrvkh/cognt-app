namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal sealed record BlockSuiteDocumentBlock(
    string Type,
    string Text,
    bool? Checked = null,
    string? Language = null)
{
    internal static BlockSuiteDocumentBlock PlainText(string text)
    {
        return new("text", text);
    }
}
