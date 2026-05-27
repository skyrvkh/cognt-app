namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextAuthoring
{
    public static CardContext CreateContextForSelectedBlocks(
        IReadOnlyList<string> contextBlockIds,
        string? title = null,
        int order = 0)
    {
        ArgumentNullException.ThrowIfNull(contextBlockIds);

        string[] normalizedBlockIds = NormalizeSelectedBlockIds(contextBlockIds);

        if (normalizedBlockIds.Length == 0)
        {
            throw new ArgumentException("At least one selected block id is required.", nameof(contextBlockIds));
        }

        return new CardContext(
            NewStableId("context"),
            NormalizeOptionalText(title),
            normalizedBlockIds,
            [],
            [],
            [],
            CardPresentationPolicies.AppDefault,
            CardContextStatus.Draft,
            null,
            order);
    }

    private static string[] NormalizeSelectedBlockIds(IReadOnlyList<string> contextBlockIds)
    {
        HashSet<string> seenBlockIds = new(StringComparer.Ordinal);
        List<string> normalizedBlockIds = new(contextBlockIds.Count);

        foreach (string blockId in contextBlockIds)
        {
            if (string.IsNullOrWhiteSpace(blockId))
            {
                continue;
            }

            string normalizedBlockId = blockId.Trim();
            if (seenBlockIds.Add(normalizedBlockId))
            {
                normalizedBlockIds.Add(normalizedBlockId);
            }
        }

        return normalizedBlockIds.ToArray();
    }

    public static CardContextDraftNoteDocument EnsureDefaultContext(CardContextDraftNoteDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Contexts.Count > 0)
        {
            return document;
        }

        return document with
        {
            Contexts =
            [
                new CardContext(
                    NewStableId("context"),
                    null,
                    [],
                    [],
                    [],
                    [],
                    CardPresentationPolicies.AppDefault,
                    CardContextStatus.Draft,
                    null)
            ],
        };
    }

    private static IReadOnlyList<string> AppendDistinctOrdinal(IReadOnlyList<string> values, string value)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<string> result = new(values.Count + 1);

        foreach (string existing in values)
        {
            if (seen.Add(existing))
            {
                result.Add(existing);
            }
        }

        if (seen.Add(value))
        {
            result.Add(value);
        }

        return result.ToArray();
    }
}
