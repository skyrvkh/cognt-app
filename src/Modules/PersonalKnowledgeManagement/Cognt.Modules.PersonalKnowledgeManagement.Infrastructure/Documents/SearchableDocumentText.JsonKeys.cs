namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class SearchableDocumentText
{
    private static readonly HashSet<string> JsonTextKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "text",
        "title",
        "caption",
        "name",
        "description",
        "insert"
    };

    private static bool IsUserTextProperty(string? propertyName)
    {
        return !string.IsNullOrWhiteSpace(propertyName) && JsonTextKeys.Contains(propertyName);
    }
}
