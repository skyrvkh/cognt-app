using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CogntApp.ViewModels;

internal static class NoteOutlineParser
{
    private const string BlockSuiteDocumentKind = "cognt-blocksuite-doc";
    private static readonly Regex MarkdownHeadingRegex = new(@"^\s{0,3}(#{1,6})\s+(.+?)\s*#*\s*$", RegexOptions.Compiled);

    internal static IReadOnlyList<NoteOutlineItemViewModel> Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        return TryParseBlockSuiteOutline(content, out IReadOnlyList<NoteOutlineItemViewModel> blockSuiteOutline)
            ? blockSuiteOutline
            : ParseMarkdownOutline(content);
    }

    private static bool TryParseBlockSuiteOutline(
        string content,
        out IReadOnlyList<NoteOutlineItemViewModel> outline)
    {
        outline = [];

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !string.Equals(GetString(root, "kind"), BlockSuiteDocumentKind, StringComparison.Ordinal)
                || !TryGetObject(root, "snapshot", out JsonElement snapshot)
                || !TryGetObject(snapshot, "blocks", out JsonElement blocks))
            {
                return false;
            }

            List<NoteOutlineItemViewModel> items = [];
            AppendHeadings(blocks, items);
            outline = items;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyList<NoteOutlineItemViewModel> ParseMarkdownOutline(string content)
    {
        List<NoteOutlineItemViewModel> items = [];
        int generatedId = 0;
        foreach (string line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            Match match = MarkdownHeadingRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            int level = Math.Clamp(match.Groups[1].Value.Length, 1, 6);
            string title = StripInlineMarkdown(match.Groups[2].Value);
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            generatedId++;
            items.Add(new NoteOutlineItemViewModel(title, level, $"markdown-heading-{generatedId}"));
        }

        return items;
    }

    private static void AppendHeadings(JsonElement block, List<NoteOutlineItemViewModel> items)
    {
        if (block.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (TryGetObject(block, "props", out JsonElement props)
            && TryGetHeadingLevel(GetString(props, "type"), out int headingLevel))
        {
            string title = ReadBlockSuiteText(props);
            if (!string.IsNullOrWhiteSpace(title))
            {
                items.Add(new NoteOutlineItemViewModel(
                    title,
                    headingLevel,
                    GetString(block, "id") ?? string.Empty));
            }
        }

        if (!block.TryGetProperty("children", out JsonElement children)
            || children.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement child in children.EnumerateArray())
        {
            AppendHeadings(child, items);
        }
    }

    private static string ReadBlockSuiteText(JsonElement props)
    {
        if (!props.TryGetProperty("text", out JsonElement textElement))
        {
            return string.Empty;
        }

        if (textElement.ValueKind == JsonValueKind.String)
        {
            return textElement.GetString() ?? string.Empty;
        }

        if (textElement.ValueKind != JsonValueKind.Object
            || !textElement.TryGetProperty("delta", out JsonElement deltaElement)
            || deltaElement.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (JsonElement operation in deltaElement.EnumerateArray())
        {
            if (operation.ValueKind == JsonValueKind.Object
                && operation.TryGetProperty("insert", out JsonElement insert)
                && insert.ValueKind == JsonValueKind.String)
            {
                builder.Append(insert.GetString());
            }
        }

        return builder.ToString().Trim();
    }

    private static bool TryGetHeadingLevel(string? blockType, out int level)
    {
        level = 0;
        if (string.IsNullOrWhiteSpace(blockType)
            || blockType.Length != 2
            || blockType[0] != 'h'
            || !char.IsDigit(blockType[1]))
        {
            return false;
        }

        level = Math.Clamp(blockType[1] - '0', 1, 6);
        return true;
    }

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement property)
    {
        return element.TryGetProperty(propertyName, out property)
            && property.ValueKind == JsonValueKind.Object;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static string StripInlineMarkdown(string text)
    {
        return text
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("*", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim();
    }
}
