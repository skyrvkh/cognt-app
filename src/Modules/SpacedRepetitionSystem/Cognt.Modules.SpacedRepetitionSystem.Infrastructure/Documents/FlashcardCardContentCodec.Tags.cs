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
        string[] mergedTags = NormalizeTags(existingTags.Concat(tags));
        if (existingTags.SequenceEqual(mergedTags, StringComparer.OrdinalIgnoreCase))
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
        string[] remainingTags = existingTags
            .Where(tag => !tagsToRemove.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (existingTags.SequenceEqual(remainingTags, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        updatedContent = SerializeDocument(document with { Tags = remainingTags });
        return true;
    }
}
