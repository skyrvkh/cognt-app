using System.Text.Json;
using System.Text.Json.Nodes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static string ApplyBlockSuiteDocumentTitle(
        string content,
        string title)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        try
        {
            JsonNode? node = JsonNode.Parse(content);
            if (node is not JsonObject root)
            {
                return content;
            }

            bool changed = false;
            JsonObject? snapshot = root["snapshot"] as JsonObject;
            JsonObject? meta = snapshot?["meta"] as JsonObject;
            if (meta is not null)
            {
                meta["title"] = title;
                changed = true;
            }

            JsonObject? blocks = snapshot?["blocks"] as JsonObject;
            JsonObject? props = blocks?["props"] as JsonObject;
            if (props is not null)
            {
                props["title"] = CreateBlockSuiteTextValue(title);
                changed = true;
            }

            return changed
                ? root.ToJsonString(BlockSuiteJsonOptions) + Environment.NewLine
                : content;
        }
        catch (JsonException)
        {
            return content;
        }
    }

    private static bool IsBlockSuiteDocumentContent(
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            return JsonNode.Parse(content) is JsonObject root
                && string.Equals(root["kind"]?.GetValue<string>(), BlockSuiteDocumentContent.Kind, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonObject CreateBlockSuiteTextValue(string value)
    {
        JsonArray delta = [];
        if (!string.IsNullOrEmpty(value))
        {
            delta.Add(new JsonObject
            {
                ["insert"] = value
            });
        }

        return new JsonObject
        {
            ["$blocksuite:internal:text$"] = true,
            ["delta"] = delta
        };
    }
}
