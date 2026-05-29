using System.Text;
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
            frontReadModel.ImageOcclusion is not null || backReadModel.ImageOcclusion is not null,
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
        Dictionary<int, CardSlot> slotsByDisplayIndex = CreateSlotsByDisplayIndex(slots);
        Dictionary<string, string> targetSlots = CreateTargetSlots(targets);

        IReadOnlyList<MultimodalTextSegmentReadModel> textSegments = BuildClozeAwareTextSegments(
            contextText,
            slotsByDisplayIndex,
            targets,
            currentSlotId,
            presentation,
            isAnswerVisible);
        string renderedContextText = RenderContextText(textSegments);
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

    private static Dictionary<int, CardSlot> CreateSlotsByDisplayIndex(IReadOnlyList<CardSlot> slots)
    {
        Dictionary<int, CardSlot> slotsByDisplayIndex = [];
        for (int index = 0; index < slots.Count; index++)
        {
            CardSlot slot = slots[index];
            if (slot.DisplayClozeIndex > 0 && !slot.IsDisabled)
            {
                slotsByDisplayIndex.TryAdd(slot.DisplayClozeIndex, slot);
            }
        }

        return slotsByDisplayIndex;
    }

    private static Dictionary<string, string> CreateTargetSlots(IReadOnlyList<CardTarget> targets)
    {
        Dictionary<string, string> targetSlots = new(StringComparer.Ordinal);
        for (int index = 0; index < targets.Count; index++)
        {
            CardTarget target = targets[index];
            if (!target.IsDisabled)
            {
                targetSlots.Add(target.Id, target.SlotId);
            }
        }

        return targetSlots;
    }

    private static string RenderContextText(IReadOnlyList<MultimodalTextSegmentReadModel> textSegments)
    {
        if (textSegments.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        for (int index = 0; index < textSegments.Count; index++)
        {
            builder.Append(textSegments[index].Text);
        }

        return builder.ToString();
    }
}
