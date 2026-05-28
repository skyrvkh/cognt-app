using System.Text.Json;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class SearchableDocumentText
{
    private static void CollectJsonText(
        JsonElement element,
        string? currentPropertyName,
        ICollection<string> values)
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
}
