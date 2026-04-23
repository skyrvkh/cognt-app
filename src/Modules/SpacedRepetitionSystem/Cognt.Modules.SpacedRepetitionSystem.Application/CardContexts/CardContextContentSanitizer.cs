using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextContentSanitizer
{
    private const string SyntheticUntitledTitle = "Untitled";

    public static string RemoveSyntheticUntitledTitle(string exportedContextContent)
    {
        return RemoveSyntheticUntitledTitle(exportedContextContent, selectedBlockIds: null);
    }

    public static bool IsSyntheticUntitledTitleBlock(object block)
    {
        return block is JsonObject jsonObject
            && IsSyntheticUntitledTitleBlock(jsonObject, selectedBlockIds: null);
    }

    internal static string RemoveSyntheticUntitledTitle(
        string exportedContextContent,
        IReadOnlySet<string>? selectedBlockIds)
    {
        if (string.IsNullOrWhiteSpace(exportedContextContent))
        {
            return string.Empty;
        }

        try
        {
            JsonNode? rootNode = JsonNode.Parse(exportedContextContent);
            if (rootNode is not JsonObject root)
            {
                return exportedContextContent;
            }

            JsonObject? snapshot = root["snapshot"] as JsonObject;
            RemoveSyntheticMetaTitle(snapshot);
            JsonObject? page = snapshot?["blocks"] as JsonObject;
            RemoveSyntheticPageTitleProperty(page);
            RemoveSyntheticNoteTitleChildren(page, selectedBlockIds);

            return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        }
        catch (JsonException)
        {
            return exportedContextContent;
        }
    }

    private static void RemoveSyntheticMetaTitle(JsonObject? snapshot)
    {
        JsonObject? meta = snapshot?["meta"] as JsonObject;
        if (meta is null)
        {
            return;
        }

        if (string.Equals(ReadString(meta["title"]), SyntheticUntitledTitle, StringComparison.Ordinal))
        {
            meta["title"] = string.Empty;
        }
    }

    private static void RemoveSyntheticPageTitleProperty(JsonObject? block)
    {
        if (block is null)
        {
            return;
        }

        if (string.Equals(ReadString(block["flavour"]), "affine:page", StringComparison.Ordinal)
            && block["props"] is JsonObject props
            && HasBlockSuiteTextValue(props["title"], SyntheticUntitledTitle))
        {
            props.Remove("title");
        }

        if (block["children"] is not JsonArray children)
        {
            return;
        }

        foreach (JsonObject child in children.OfType<JsonObject>())
        {
            RemoveSyntheticPageTitleProperty(child);
        }
    }
}
