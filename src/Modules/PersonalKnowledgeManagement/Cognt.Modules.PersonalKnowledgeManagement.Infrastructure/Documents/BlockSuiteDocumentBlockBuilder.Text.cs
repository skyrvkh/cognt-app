using System.Text.RegularExpressions;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class BlockSuiteDocumentBlockBuilder
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    internal static string NormalizeText(string value)
    {
        return WhitespaceRegex
            .Replace(value.Replace('\u00A0', ' '), " ")
            .Trim();
    }
}
