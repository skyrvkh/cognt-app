using System.Text.Json.Nodes;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextSnapshotFactory
{
    private static string ExtractSelectedContextContent(string noteContent, CardContext context)
    {
        if (string.IsNullOrWhiteSpace(noteContent))
        {
            return string.Empty;
        }

        HashSet<string> contextIds = BuildContextIdSet(context);

        if (contextIds.Count == 0)
        {
            return string.Empty;
        }

        try
        {
            JsonNode? rootNode = JsonNode.Parse(noteContent);
            if (rootNode is not JsonObject root)
            {
                return CardContextContentSanitizer.RemoveSyntheticUntitledTitle(noteContent);
            }

            JsonObject? snapshot = root["snapshot"] as JsonObject;
            JsonObject? blocks = snapshot?["blocks"] as JsonObject;
            JsonObject? note = FindFirstBlockByFlavour(blocks, "affine:note");
            JsonArray? children = note?["children"] as JsonArray;
            if (children is null)
            {
                return string.Empty;
            }

            List<JsonNode> filteredChildren = [];
            foreach (JsonNode? childNode in children)
            {
                if (childNode is JsonObject child && ContainsSelectedContextBlock(child, contextIds))
                {
                    filteredChildren.Add(child.DeepClone());
                }
            }

            if (filteredChildren.Count == 0)
            {
                return string.Empty;
            }

            JsonObject clonedRoot = (JsonObject)root.DeepClone();
            JsonObject? clonedSnapshot = clonedRoot["snapshot"] as JsonObject;
            JsonObject? clonedBlocks = clonedSnapshot?["blocks"] as JsonObject;
            JsonObject? clonedNote = FindFirstBlockByFlavour(clonedBlocks, "affine:note");
            if (clonedNote?["children"] is not JsonArray clonedChildren)
            {
                return string.Empty;
            }

            clonedChildren.Clear();
            foreach (JsonNode child in filteredChildren)
            {
                clonedChildren.Add(child);
            }

            string filteredContent = clonedRoot.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
                + Environment.NewLine;
            return CardContextContentSanitizer.RemoveSyntheticUntitledTitle(filteredContent, contextIds);
        }
        catch
        {
            return CardContextContentSanitizer.RemoveSyntheticUntitledTitle(noteContent);
        }
    }

    private static HashSet<string> BuildContextIdSet(CardContext context)
    {
        HashSet<string> contextIds = new(StringComparer.Ordinal);
        AddNonWhiteSpaceIds(contextIds, context.ContextBlockIds);

        foreach (CardTarget target in context.Targets)
        {
            foreach (TextAnchor anchor in target.TextAnchors)
            {
                if (!string.IsNullOrWhiteSpace(anchor.BlockId))
                {
                    contextIds.Add(anchor.BlockId);
                }
            }
        }

        foreach (CardContextImageSurface image in context.Images)
        {
            if (!string.IsNullOrWhiteSpace(image.ImageBlockId))
            {
                contextIds.Add(image.ImageBlockId);
            }
        }

        return contextIds;
    }

    private static void AddNonWhiteSpaceIds(HashSet<string> contextIds, IReadOnlyList<string> ids)
    {
        foreach (string id in ids)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                contextIds.Add(id);
            }
        }
    }

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
