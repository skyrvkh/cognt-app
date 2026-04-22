using System.Text.Json;

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

    internal static string ExtractJsonPlainText(string content)
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

    private static void CollectJsonText(JsonElement element, string? currentPropertyName, ICollection<string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    CollectJsonText(property.Value, property.Name, values);
                }

                break;

            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    CollectJsonText(item, currentPropertyName, values);
                }

                break;

            case JsonValueKind.String:
                if (IsUserTextProperty(currentPropertyName))
                {
                    string? value = element.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value);
                    }
                }

                break;
        }
    }

    private static bool IsUserTextProperty(string? propertyName)
    {
        return !string.IsNullOrWhiteSpace(propertyName) && JsonTextKeys.Contains(propertyName);
    }
}
