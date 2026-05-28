namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class SearchableMarkdownText
{
    internal static string ExtractPlainText(string content)
    {
        string text = CodeFenceMarkerRegex.Replace(content, " ");
        text = MarkdownLinkRegex.Replace(text, "$1");
        text = WikiLinkRegex.Replace(text, match =>
            match.Groups[2].Success ? match.Groups[2].Value : match.Groups[1].Value);
        text = HtmlTagRegex.Replace(text, " ");
        text = MarkdownSyntaxRegex.Replace(text, " ");
        return SearchableDocumentText.NormalizeWhitespace(text);
    }
}
