using System.Text.Json.Nodes;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextQueueBuilder
{
    private static IReadOnlySet<string> LoadAvailableBlockIds(string noteContent)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(noteContent))
        {
            return ids;
        }

        try
        {
            JsonNode? rootNode = JsonNode.Parse(noteContent);
            JsonObject? blocks = rootNode?["snapshot"]?["blocks"] as JsonObject;
            VisitBlocks(blocks, ids);
        }
        catch
        {
            return ids;
        }

        return ids;
    }

    private static void VisitBlocks(JsonObject? block, ISet<string> ids)
    {
        if (block is null)
        {
            return;
        }

        string? blockId = block["id"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(blockId))
        {
            ids.Add(blockId);
        }

        if (block["children"] is not JsonArray children)
        {
            return;
        }

        foreach (JsonObject child in children.OfType<JsonObject>())
        {
            VisitBlocks(child, ids);
        }
    }
}
