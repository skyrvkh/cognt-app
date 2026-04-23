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

        HashSet<string> contextIds = context.ContextBlockIds
            .Concat(context.Targets.SelectMany(target => target.TextAnchors).Select(anchor => anchor.BlockId))
            .Concat(context.Images.Select(image => image.ImageBlockId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

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

            JsonNode[] filteredChildren = children
                .OfType<JsonObject>()
                .Where(child => ContainsSelectedContextBlock(child, contextIds))
                .Select(child => child.DeepClone())
                .ToArray();

            if (filteredChildren.Length == 0)
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

        foreach (JsonObject child in children.OfType<JsonObject>())
        {
            JsonObject? match = FindFirstBlockByFlavour(child, flavour);
            if (match is not null)
            {
                return match;
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

        return children
            .OfType<JsonObject>()
            .Any(child => ContainsSelectedContextBlock(child, contextIds));
    }
}
