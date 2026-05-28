using System.Text.Json;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class SearchableDocumentText
{
    internal static string ExtractJsonPlainText(
        string content,
        string? documentPath = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            List<string> values = [];
            CollectJsonText(document.RootElement, currentPropertyName: null, values);
            return NormalizeWhitespace(string.Join(' ', values));
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}
