namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

internal static partial class CardContextSelectedContentExtractor
{
    private static HashSet<string> BuildContextIdSet(CardContext context)
    {
        HashSet<string> contextIds = new(StringComparer.Ordinal);
        AddNonWhiteSpaceIds(contextIds, context.ContextBlockIds);

        foreach (CardTarget target in context.Targets)
        {
            foreach (TextAnchor anchor in target.TextAnchors)
            {
                if (!string.IsNullOrWhiteSpace(anchor.BlockId))
                {
                    contextIds.Add(anchor.BlockId);
                }
            }
        }

        foreach (CardContextImageSurface image in context.Images)
        {
            if (!string.IsNullOrWhiteSpace(image.ImageBlockId))
            {
                contextIds.Add(image.ImageBlockId);
            }
        }

        return contextIds;
    }

    private static void AddNonWhiteSpaceIds(HashSet<string> contextIds, IReadOnlyList<string> ids)
    {
        foreach (string id in ids)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                contextIds.Add(id);
            }
        }
    }
}
