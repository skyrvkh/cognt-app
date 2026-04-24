using System.Text;
using System.Text.Json;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static class BlockSuitePlainTextReader
{
    internal static string Read(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            StringBuilder builder = new();
            AppendTextValues(document.RootElement, builder);
            return builder.ToString().Trim();
        }
        catch (JsonException)
        {
            return content.Trim();
        }
    }

    private static void AppendTextValues(JsonElement element, StringBuilder builder)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (IsPageBlock(element))
            {
                if (element.TryGetProperty("children", out JsonElement pageChildren))
                {
                    AppendTextValues(pageChildren, builder);
                }

                return;
            }

            if (TryAppendBlockSuiteText(element, builder))
            {
                return;
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                AppendTextValues(property.Value, builder);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                AppendTextValues(child, builder);
            }
        }
    }

    private static bool IsPageBlock(JsonElement element)
    {
        return element.TryGetProperty("flavour", out JsonElement flavour)
            && flavour.ValueKind == JsonValueKind.String
            && string.Equals(flavour.GetString(), "affine:page", StringComparison.Ordinal);
    }

    private static bool TryAppendBlockSuiteText(JsonElement element, StringBuilder builder)
    {
        if (!element.TryGetProperty("$blocksuite:internal:text$", out JsonElement marker)
            || marker.ValueKind != JsonValueKind.True
            || !element.TryGetProperty("delta", out JsonElement delta)
            || delta.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        AppendDelta(delta, builder);
        return true;
    }

    private static void AppendDelta(JsonElement delta, StringBuilder builder)
    {
        foreach (JsonElement operation in delta.EnumerateArray())
        {
            if (operation.ValueKind != JsonValueKind.Object
                || !operation.TryGetProperty("insert", out JsonElement insert)
                || insert.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(insert.GetString());
        }
    }
}
