using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class SearchableDocumentText
{
    private static readonly Regex MarkdownCodeFenceMarkerRegex = new(@"^[ \t]*`{3,}.*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex MarkdownLinkRegex = new(@"\[([^\]\r\n]+)\]\([^)]+\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WikiLinkRegex = new(@"!?\[\[([^\]\[\r\n|]+)(?:\|([^\]\[\r\n]+))?\]\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownSyntaxRegex = new(@"[`*_~>#-]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    internal static string ExtractPlainText(string documentPath, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        string extension = Path.GetExtension(documentPath);
        return string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
            ? ExtractJsonPlainText(content)
            : ExtractMarkdownPlainText(content);
    }

    internal static string NormalizeWhitespace(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespaceRegex.Replace(value, " ").Trim();
    }

    internal static string CreateBoundedSnippet(string text, int start, int length, int radius)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        int safeStart = Math.Clamp(start, 0, text.Length);
        int safeLength = Math.Clamp(length, 0, text.Length - safeStart);
        int snippetStart = Math.Max(0, safeStart - Math.Max(0, radius));
        int snippetEnd = Math.Min(text.Length, safeStart + safeLength + Math.Max(0, radius));
        string prefix = snippetStart > 0 ? "... " : string.Empty;
        string suffix = snippetEnd < text.Length ? " ..." : string.Empty;

        return $"{prefix}{NormalizeWhitespace(text[snippetStart..snippetEnd])}{suffix}";
    }

    private static string ExtractMarkdownPlainText(string content)
    {
        string text = MarkdownCodeFenceMarkerRegex.Replace(content, " ");
        text = MarkdownLinkRegex.Replace(text, "$1");
        text = WikiLinkRegex.Replace(text, match =>
            match.Groups[2].Success ? match.Groups[2].Value : match.Groups[1].Value);
        text = HtmlTagRegex.Replace(text, " ");
        text = MarkdownSyntaxRegex.Replace(text, " ");
        return NormalizeWhitespace(text);
    }
}
