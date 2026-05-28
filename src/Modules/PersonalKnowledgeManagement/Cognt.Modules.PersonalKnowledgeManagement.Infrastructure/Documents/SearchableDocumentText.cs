namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class SearchableDocumentText
{
    internal static string ExtractPlainText(
        string documentPath,
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        string extension = Path.GetExtension(documentPath);
        return string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
            ? ExtractJsonPlainText(content, documentPath)
            : SearchableMarkdownText.ExtractPlainText(content);
    }
}
