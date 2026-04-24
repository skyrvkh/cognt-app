using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    internal static string CreateBasicDocument(
        string frontContent,
        string backContent,
        IReadOnlyList<string> tags)
    {
        return SerializeDocument(
            new FlashcardCardDocument(
                Schema,
                null,
                Version,
                BasicCardType,
                frontContent,
                backContent,
                null,
                NormalizeTags(tags),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));
    }

    internal static string CreateClozeDocument(
        string sourceContent,
        int clozeIndex,
        IReadOnlyList<string> tags)
    {
        return SerializeDocument(
            new FlashcardCardDocument(
                Schema,
                null,
                Version,
                ClozeCardType,
                sourceContent,
                null,
                clozeIndex,
                NormalizeTags(tags),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));
    }

    internal static string CreateMultimodalClozeDocument(
        ExpandedCardContextCard card,
        IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(card);

        return SerializeDocument(
            new FlashcardCardDocument(
                Schema,
                null,
                Version,
                MultimodalClozeCardType,
                null,
                null,
                null,
                NormalizeTags(tags),
                new FlashcardCardSourceDocument(
                    "note",
                    card.SourceRelPath,
                    card.ContextId,
                    card.CurrentSlotId,
                    card.CurrentTargetIds,
                    card.SnapshotHash),
                null,
                card.Presentation,
                card.ContextContent,
                card.Slots,
                null,
                card.Targets,
                card.Images,
                card.CurrentSlotId));
    }
}
