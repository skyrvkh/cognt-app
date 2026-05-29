using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextContentSanitizer
{
    private const string SyntheticUntitledTitle = "Untitled";
    private static readonly Regex ClozeMarkupExpression = new(
        @"\{\{c\d+::(?<text>.*?)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    public static string RemoveSyntheticUntitledTitle(string exportedContextContent)
    {
        return RemoveSyntheticUntitledTitle(exportedContextContent, selectedBlockIds: null);
    }

    public static bool IsSyntheticUntitledTitleBlock(object block)
    {
        return block is JsonObject jsonObject
            && IsSyntheticUntitledTitleBlock(jsonObject, selectedBlockIds: null);
    }

    public static string RemoveClozeMarkup(string exportedContent)
    {
        if (string.IsNullOrWhiteSpace(exportedContent))
        {
            return string.Empty;
        }

        try
        {
            JsonNode? rootNode = JsonNode.Parse(exportedContent);
            if (rootNode is null)
            {
                return RemoveClozeMarkupFromText(exportedContent);
            }

            RemoveClozeMarkup(rootNode);
            return rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        }
        catch (JsonException)
        {
            return RemoveClozeMarkupFromText(exportedContent);
        }
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

    private static void RemoveClozeMarkup(JsonNode node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (KeyValuePair<string, JsonNode?> property in jsonObject.ToArray())
            {
                if (property.Value is null)
                {
                    continue;
                }

                if (property.Value is JsonValue jsonValue && TryReadString(jsonValue, out string value))
                {
                    string cleaned = RemoveClozeMarkupFromText(value);
                    if (!string.Equals(cleaned, value, StringComparison.Ordinal))
                    {
                        jsonObject[property.Key] = cleaned;
                    }

                    continue;
                }

                RemoveClozeMarkup(property.Value);
            }

            return;
        }

        if (node is JsonArray jsonArray)
        {
            for (int index = 0; index < jsonArray.Count; index++)
            {
                if (jsonArray[index] is not JsonNode child)
                {
                    continue;
                }

                if (child is JsonValue jsonValue && TryReadString(jsonValue, out string value))
                {
                    string cleaned = RemoveClozeMarkupFromText(value);
                    if (!string.Equals(cleaned, value, StringComparison.Ordinal))
                    {
                        jsonArray[index] = cleaned;
                    }

                    continue;
                }

                RemoveClozeMarkup(child);
            }
        }
    }

    private static bool TryReadString(JsonValue value, out string text)
    {
        try
        {
            text = value.GetValue<string>() ?? string.Empty;
            return true;
        }
        catch (InvalidOperationException)
        {
            text = string.Empty;
            return false;
        }
    }

    private static string RemoveClozeMarkupFromText(string value)
    {
        return ClozeMarkupExpression.Replace(value, match => match.Groups["text"].Value);
    }
}
