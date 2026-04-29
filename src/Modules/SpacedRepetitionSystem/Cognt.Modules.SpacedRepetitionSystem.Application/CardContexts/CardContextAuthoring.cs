namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextAuthoring
{
    public static CardContext CreateContextForSelectedBlocks(
        IReadOnlyList<string> contextBlockIds,
        string? title = null,
        int order = 0)
    {
        ArgumentNullException.ThrowIfNull(contextBlockIds);

        string[] normalizedBlockIds = contextBlockIds
            .Where(blockId => !string.IsNullOrWhiteSpace(blockId))
            .Select(blockId => blockId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

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
}
