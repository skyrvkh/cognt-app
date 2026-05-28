using System.Text.RegularExpressions;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class SearchableMarkdownText
{
    private static readonly Regex CodeFenceMarkerRegex = new(@"^[ \t]*`{3,}.*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex MarkdownLinkRegex = new(@"\[([^\]\r\n]+)\]\([^)]+\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WikiLinkRegex = new(@"!?\[\[([^\]\[\r\n|]+)(?:\|([^\]\[\r\n]+))?\]\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownSyntaxRegex = new(@"[`*_~>#-]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
