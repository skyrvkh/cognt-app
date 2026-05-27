using System.Text;
using System.Text.Json.Nodes;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextContentSanitizer
{
    private static void RemoveSyntheticNoteTitleChildren(
        JsonObject? block,
        IReadOnlySet<string>? selectedBlockIds)
    {
        JsonObject? note = FindFirstBlockByFlavour(block, "affine:note");
        if (note?["children"] is not JsonArray children || children.Count == 0)
        {
            return;
        }

        for (int index = children.Count - 1; index >= 0; index--)
        {
            if (children[index] is not JsonObject child)
            {
                continue;
            }

            if (IsSyntheticUntitledTitleBlock(child, selectedBlockIds))
            {
                children.RemoveAt(index);
            }
        }
    }

    private static bool IsSyntheticUntitledTitleBlock(
        JsonObject block,
        IReadOnlySet<string>? selectedBlockIds)
    {
        string? id = ReadString(block["id"]);
        if (!string.IsNullOrWhiteSpace(id) && selectedBlockIds?.Contains(id) == true)
        {
            return false;
        }

        string? flavour = ReadString(block["flavour"]);
        if (!string.Equals(flavour, "affine:paragraph", StringComparison.Ordinal)
            && !string.Equals(flavour, "affine:heading", StringComparison.Ordinal))
        {
            return false;
        }

        if (block["children"] is JsonArray { Count: > 0 })
        {
            return false;
        }

        return block["props"] is JsonObject props
            && HasBlockSuiteTextValue(props["text"], SyntheticUntitledTitle);
    }

    private static JsonObject? FindFirstBlockByFlavour(JsonObject? block, string flavour)
    {
        if (block is null)
        {
            return null;
        }

        if (string.Equals(ReadString(block["flavour"]), flavour, StringComparison.Ordinal))
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

    private static bool HasBlockSuiteTextValue(JsonNode? node, string expectedText)
    {
        if (node is not JsonObject text
            || text["$blocksuite:internal:text$"] is null
            || text["delta"] is not JsonArray delta)
        {
            return false;
        }

        StringBuilder value = new();
        foreach (JsonNode? operationNode in delta)
        {
            if (operationNode is JsonObject operation)
            {
                string? insert = ReadString(operation["insert"]);
                if (insert is not null)
                {
                    value.Append(insert);
                }
            }
        }

        return string.Equals(value.ToString().Trim(), expectedText, StringComparison.Ordinal);
    }

    private static string? ReadString(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
