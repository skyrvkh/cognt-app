namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Tags;

internal static partial class KnowledgeTagNormalizer
{
    internal static string[] NormalizeIdentities(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (tags.Count == 0)
        {
            return [];
        }

        HashSet<string> seenTags = new(StringComparer.Ordinal);
        List<string> normalizedTags = new(tags.Count);
        for (int index = 0; index < tags.Count; index++)
        {
            string normalizedTag = NormalizeIdentity(tags[index]);
            if (normalizedTag.Length > 0 && seenTags.Add(normalizedTag))
            {
                normalizedTags.Add(normalizedTag);
            }
        }

        if (normalizedTags.Count == 0)
        {
            return [];
        }

        return normalizedTags.ToArray();
    }

    private static string NormalizeIdentity(string? tag)
    {
        return NormalizeDisplayName(tag).ToUpperInvariant();
    }
}
