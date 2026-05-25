using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    private const string HiddenClozeText = "[[...]]";

    private static MaterializedFlashcardContent MaterializeMultimodal(
        FlashcardCardDocument document,
        string? collectionRoot)
    {
        string contextContent = document.ContextContent ?? string.Empty;
        string contextText = BlockSuiteDocumentText.ToPlainText(contextContent);
        IReadOnlyList<CardSlot> slots = document.Slots ?? document.Groups ?? [];
        IReadOnlyList<CardTarget> targets = document.Targets ?? [];
        string currentSlotId = document.CurrentSlotId ?? string.Empty;
        CardPresentationPolicy presentation = document.Presentation
            ?? CardPresentationPolicies.FromLegacyCueModes(document.FrontPolicy?.Text, document.FrontPolicy?.Image)
            ?? CardPresentationPolicies.AppDefault;

        MultimodalClozeReadModel frontReadModel = BuildMultimodalReadModel(
            contextText,
            document.Images,
            slots,
            targets,
            currentSlotId,
            presentation,
            collectionRoot,
            isAnswerVisible: false,
            out bool missingFrontMedia);
        MultimodalClozeReadModel backReadModel = BuildMultimodalReadModel(
            contextText,
            document.Images,
            slots,
            targets,
            currentSlotId,
            presentation,
            collectionRoot,
            isAnswerVisible: true,
            out bool missingBackMedia);

        FlashcardMaterializationStatus status = missingFrontMedia || missingBackMedia
            ? FlashcardMaterializationStatus.ReferencedMediaMissing
            : FlashcardMaterializationStatus.Ok;

        return new MaterializedFlashcardContent(
            contextContent,
            contextContent,
            frontReadModel.ContextText,
            backReadModel.ContextText,
            contextContent,
            NormalizeTags(document.Tags),
            FlashcardContentKind.MultimodalCloze,
            FlashcardContentKind.MultimodalCloze,
            status,
            MultimodalClozeCardType,
            frontReadModel,
            backReadModel);
    }

    private static MultimodalClozeReadModel BuildMultimodalReadModel(
        string contextText,
        IReadOnlyList<CardContextImageSurface>? images,
        IReadOnlyList<CardSlot> slots,
        IReadOnlyList<CardTarget> targets,
        string currentSlotId,
        CardPresentationPolicy presentation,
        string? collectionRoot,
        bool isAnswerVisible,
        out bool missingMedia)
    {
        Dictionary<int, CardSlot> slotsByDisplayIndex = slots
            .Where(slot => slot.DisplayClozeIndex > 0 && !slot.IsDisabled)
            .GroupBy(slot => slot.DisplayClozeIndex)
            .Select(grouping => grouping.First())
            .ToDictionary(slot => slot.DisplayClozeIndex, slot => slot);
        Dictionary<string, string> targetSlots = targets
            .Where(target => !target.IsDisabled)
            .ToDictionary(target => target.Id, target => target.SlotId, StringComparer.Ordinal);

        IReadOnlyList<MultimodalTextSegmentReadModel> textSegments = BuildClozeAwareTextSegments(
            contextText,
            slotsByDisplayIndex,
            currentSlotId,
            presentation,
            isAnswerVisible);
        string renderedContextText = string.Concat(textSegments.Select(segment => segment.Text));
        ImageOcclusionReadModel? imageOcclusion = BuildImageOcclusionReadModel(
            images,
            targetSlots,
            currentSlotId,
            presentation,
            collectionRoot,
            isAnswerVisible,
            out missingMedia);

        return new MultimodalClozeReadModel(
            textSegments,
            renderedContextText,
            imageOcclusion,
            presentation,
            currentSlotId);
    }

}
