namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

internal static partial class SearchableDocumentText
{
    internal static string CreateBoundedSnippet(
        string text,
        int start,
        int length,
        int radius)
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
}
