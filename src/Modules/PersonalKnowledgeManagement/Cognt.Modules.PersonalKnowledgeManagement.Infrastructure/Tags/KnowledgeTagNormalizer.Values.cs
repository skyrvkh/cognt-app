namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Tags;

internal static partial class KnowledgeTagNormalizer
{
    internal static KnowledgeTagValue[] NormalizeValues(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (tags.Count == 0)
        {
            return [];
        }

        Dictionary<string, KnowledgeTagValue> normalizedByIdentity = new(StringComparer.Ordinal);
        for (int index = 0; index < tags.Count; index++)
        {
            string displayName = NormalizeDisplayName(tags[index]);
            if (displayName.Length == 0)
            {
                continue;
            }

            string normalizedName = displayName.ToUpperInvariant();
            normalizedByIdentity.TryAdd(normalizedName, new KnowledgeTagValue(displayName, normalizedName));
        }

        if (normalizedByIdentity.Count == 0)
        {
            return [];
        }

        KnowledgeTagValue[] normalizedTags = new KnowledgeTagValue[normalizedByIdentity.Count];
        int tagIndex = 0;
        foreach (KnowledgeTagValue tag in normalizedByIdentity.Values)
        {
            normalizedTags[tagIndex++] = tag;
        }

        Array.Sort(normalizedTags, static (left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.DisplayName, right.DisplayName));
        return normalizedTags;
    }
}
