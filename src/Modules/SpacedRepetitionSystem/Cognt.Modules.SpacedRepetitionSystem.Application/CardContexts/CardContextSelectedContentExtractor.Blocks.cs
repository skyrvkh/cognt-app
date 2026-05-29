using System.Text.Json.Nodes;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

internal static partial class CardContextSelectedContentExtractor
{
    private static JsonObject? FindFirstBlockByFlavour(JsonObject? block, string flavour)
    {
        if (block is null)
        {
            return null;
        }

        if (string.Equals(block["flavour"]?.GetValue<string>(), flavour, StringComparison.Ordinal))
        {
            return block;
        }

        if (block["children"] is not JsonArray children)
        {
            return null;
        }

        foreach (JsonNode? childNode in children)
        {
            if (childNode is JsonObject child)
            {
                JsonObject? match = FindFirstBlockByFlavour(child, flavour);
                if (match is not null)
                {
                    return match;
                }
            }
        }

        return null;
    }

    private static bool ContainsSelectedContextBlock(JsonObject block, IReadOnlySet<string> contextIds)
    {
        string? blockId = block["id"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(blockId) && contextIds.Contains(blockId))
        {
            return true;
        }

        if (block["children"] is not JsonArray children)
        {
            return false;
        }

        foreach (JsonNode? childNode in children)
        {
            if (childNode is JsonObject child && ContainsSelectedContextBlock(child, contextIds))
            {
                return true;
            }
        }

        return false;
    }
}
