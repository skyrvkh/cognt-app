using System.Text.Json;
using System.Text.Json.Nodes;

namespace CogntApp.ViewModels;

internal static class ExtractNoteContentNormalizer
{
    private static readonly JsonSerializerOptions BlockSuiteJsonOptions = new()
    {
        WriteIndented = true
    };

    internal static string? NormalizeBlockSuiteContent(string selectedContent)
    {
        if (string.IsNullOrWhiteSpace(selectedContent))
        {
            return null;
        }

        try
        {
            if (JsonNode.Parse(selectedContent) is not JsonObject root
                || root["kind"]?.GetValue<string>() != "cognt-blocksuite-doc"
                || root["snapshot"] is not JsonObject snapshot
                || snapshot["blocks"] is not JsonObject blocks)
            {
                return null;
            }

            if (!BlockSuiteBlockHasBodyContent(blocks, isRootBlock: true))
            {
                return null;
            }

            NormalizeExtractRootChildren(blocks);
            return root.ToJsonString(BlockSuiteJsonOptions) + Environment.NewLine;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void NormalizeExtractRootChildren(JsonObject blocks)
    {
        if (blocks["children"] is not JsonArray children)
        {
            return;
        }

        for (int index = children.Count - 1; index >= 0; index--)
        {
            if (children[index] is JsonObject child
                && string.Equals(child["flavour"]?.GetValue<string>(), "affine:surface", StringComparison.Ordinal))
            {
                children.RemoveAt(index);
            }
        }

        foreach (JsonNode? child in children)
        {
            if (child is JsonObject childBlock
                && string.Equals(childBlock["flavour"]?.GetValue<string>(), "affine:note", StringComparison.Ordinal)
                && childBlock["props"] is JsonObject props)
            {
                props["xywh"] = "[0,0,498,92]";
                props["background"] = "--affine-note-background-white";
                props["lockedBySelf"] = false;
            }
        }
    }

    private static bool BlockSuiteBlockHasBodyContent(JsonObject block, bool isRootBlock = false)
    {
        if (!isRootBlock
            && block["props"] is JsonObject props
            && BlockSuiteTextValueHasBodyContent(props["text"]))
        {
            return true;
        }

        if (block["children"] is not JsonArray children)
        {
            return false;
        }

        foreach (JsonNode? child in children)
        {
            if (child is JsonObject childBlock
                && BlockSuiteBlockHasBodyContent(childBlock))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BlockSuiteTextValueHasBodyContent(JsonNode? textValue)
    {
        if (textValue is not JsonObject textObject
            || textObject["delta"] is not JsonArray delta)
        {
            return false;
        }

        foreach (JsonNode? entryNode in delta)
        {
            if (entryNode is JsonObject entry
                && entry["insert"]?.GetValue<string>() is { } insertedText
                && !string.IsNullOrWhiteSpace(insertedText))
            {
                return true;
            }
        }

        return false;
    }
}
