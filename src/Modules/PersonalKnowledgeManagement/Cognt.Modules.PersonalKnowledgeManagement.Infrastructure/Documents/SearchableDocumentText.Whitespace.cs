using System.Text.RegularExpressions;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class SearchableDocumentText
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    internal static string NormalizeWhitespace(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespaceRegex.Replace(value, " ").Trim();
    }
}
