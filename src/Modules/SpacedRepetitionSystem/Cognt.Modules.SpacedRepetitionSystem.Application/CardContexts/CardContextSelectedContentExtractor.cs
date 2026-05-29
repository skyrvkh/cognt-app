using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

internal static partial class CardContextSelectedContentExtractor
{
    internal static string Extract(string noteContent, CardContext context)
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

            string filteredContent = clonedRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                + Environment.NewLine;
            return CardContextContentSanitizer.RemoveSyntheticUntitledTitle(filteredContent, contextIds);
        }
        catch
        {
            return CardContextContentSanitizer.RemoveSyntheticUntitledTitle(noteContent);
        }
    }

}
