using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static IReadOnlyList<string> ExtractNoteTags(
        string notePath,
        string content,
        ILogger<NoteService>? logger = null)
    {
        if (!string.Equals(Path.GetExtension(notePath), BlockSuiteNoteExtension, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        try
        {
            JsonNode? root = JsonNode.Parse(content);
            HashSet<string> tags = new(StringComparer.OrdinalIgnoreCase);
            CollectSnapshotMetaTags(root?["snapshot"]?["meta"]?["tags"], tags);
            CollectPropertyTagValuesRecursively(root, tags);
            return tags
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(
                ex,
                "Note tag extraction skipped malformed BlockSuite JSON. NotePath={NotePath}",
                notePath);
            return [];
        }
    }

    private static void CollectSnapshotMetaTags(JsonNode? node, ISet<string> tags)
    {
        if (node is not JsonArray array)
        {
            return;
        }

        foreach (JsonNode? item in array)
        {
            AddNormalizedTagValues(item?.GetValue<string>(), tags);
        }
    }

    private static void CollectPropertyTagValuesRecursively(JsonNode? node, ISet<string> tags)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj.TryGetPropertyValue("name", out JsonNode? nameNode)
                    && obj.TryGetPropertyValue("value", out JsonNode? valueNode)
                    && string.Equals(nameNode?.GetValue<string>()?.Trim(), "tags", StringComparison.OrdinalIgnoreCase))
                {
                    AddNormalizedTagValues(valueNode?.GetValue<string>(), tags);
                }

                foreach (KeyValuePair<string, JsonNode?> property in obj)
                {
                    CollectPropertyTagValuesRecursively(property.Value, tags);
                }

                break;

            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    CollectPropertyTagValuesRecursively(item, tags);
                }

                break;
        }
    }

    private static void AddNormalizedTagValues(string? value, ISet<string> tags)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (string tag in value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string normalized = SearchableDocumentText.NormalizeWhitespace(tag);
            if (normalized.Length > 0)
            {
                tags.Add(normalized);
            }
        }
    }
}
