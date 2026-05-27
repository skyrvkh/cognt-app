namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    internal static bool TryAddTags(
        string existingContent,
        IReadOnlyList<string> tags,
        out string updatedContent)
    {
        ArgumentNullException.ThrowIfNull(tags);

        updatedContent = existingContent;
        if (!TryReadDocument(existingContent, out FlashcardCardDocument? document))
        {
            return false;
        }

        string[] existingTags = NormalizeTags(document.Tags);
        string[] mergedTags = NormalizeTags(existingTags, tags);
        if (TagsEqual(existingTags, mergedTags))
        {
            return false;
        }

        updatedContent = SerializeDocument(document with { Tags = mergedTags });
        return true;
    }

    internal static bool TryRemoveTags(
        string existingContent,
        IReadOnlyList<string> tags,
        out string updatedContent)
    {
        ArgumentNullException.ThrowIfNull(tags);

        updatedContent = existingContent;
        if (!TryReadDocument(existingContent, out FlashcardCardDocument? document))
        {
            return false;
        }

        string[] tagsToRemove = NormalizeTags(tags);
        if (tagsToRemove.Length == 0)
        {
            return false;
        }

        string[] existingTags = NormalizeTags(document.Tags);
        string[] remainingTags = RemoveTags(existingTags, tagsToRemove);
        if (TagsEqual(existingTags, remainingTags))
        {
            return false;
        }

        updatedContent = SerializeDocument(document with { Tags = remainingTags });
        return true;
    }

    private static string[] RemoveTags(IReadOnlyList<string> existingTags, IReadOnlyList<string> tagsToRemove)
    {
        HashSet<string> tagsToRemoveSet = new(tagsToRemove, StringComparer.OrdinalIgnoreCase);
        List<string> remainingTags = new(existingTags.Count);
        foreach (string tag in existingTags)
        {
            if (!tagsToRemoveSet.Contains(tag))
            {
                remainingTags.Add(tag);
            }
        }

        return remainingTags.ToArray();
    }

    private static bool TagsEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
