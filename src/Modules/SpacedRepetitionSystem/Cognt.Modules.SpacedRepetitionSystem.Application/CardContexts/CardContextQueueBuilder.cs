namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextQueueBuilder
{
    public static NoteToCardQueueReadModel Build(
        string collectionId,
        string sourceRelPath,
        string noteContent,
        CardContextDraftNoteDocument document,
        IReadOnlyList<GeneratedCardContextCardReadModel> generatedCards)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelPath);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(generatedCards);

        string normalizedSourceRelPath = CardContextDraftNoteDocument.NormalizeRelPath(sourceRelPath);
        IReadOnlySet<string> availableBlockIds = LoadAvailableBlockIds(noteContent);
        Dictionary<(string ContextId, string SlotId), GeneratedCardContextCardReadModel> generatedByKey =
            BuildGeneratedCardLookup(generatedCards, normalizedSourceRelPath);

        List<CardCandidateReadModel> items = new(CountCandidateSlots(document.Contexts));
        foreach (CardContext context in document.Contexts)
        {
            IReadOnlySet<int> duplicateDisplayIndexes = FindDuplicateDisplayIndexes(context.Slots);
            Dictionary<string, CardTarget> targetsById = BuildTargetLookup(context.Targets);
            Dictionary<string, List<CardTarget>> targetsBySlotId = BuildTargetsBySlotLookup(context.Targets);
            Dictionary<string, CardContextImageSurface> imagesByBlockId = BuildImageSurfaceLookup(context.Images);
            Dictionary<(string ImageBlockId, string MaskId), ImageOcclusionMask> masksByImageAndId =
                BuildImageMaskLookup(context.Images);
            CardContextSnapshot snapshot = CardContextSnapshotFactory.CreateSnapshot(normalizedSourceRelPath, noteContent, context);
            foreach (CardSlot slot in context.Slots
                         .OrderBy(candidate => candidate.DisplayClozeIndex)
                         .ThenBy(candidate => candidate.Id, StringComparer.Ordinal))
            {
                IReadOnlyList<CardTarget> targets = ResolveEnabledTargets(slot, targetsById, targetsBySlotId);
                string snapshotHash = CardContextSnapshotFactory.CreateSlotSnapshotHash(snapshot, slot.Id);
                CandidateValidation validation = ValidateCandidate(
                    context,
                    slot,
                    targets,
                    availableBlockIds,
                    duplicateDisplayIndexes,
                    imagesByBlockId,
                    masksByImageAndId);
                generatedByKey.TryGetValue((context.Id, slot.Id), out GeneratedCardContextCardReadModel? generated);

                items.Add(new CardCandidateReadModel(
                    context.Id,
                    slot.Id,
                    slot.DisplayClozeIndex,
                    CreateTargetIdArray(targets),
                    BuildAnswerPreview(targets),
                    BuildTextAnchorPreviews(targets),
                    BuildImageAnchorPreviews(imagesByBlockId, masksByImageAndId, targets),
                    DetermineStatus(context, slot, validation, generated, snapshotHash),
                    validation.Message,
                    snapshotHash));
            }
        }

        NoteToCardQueueSummaryReadModel summary = BuildSummary(items);
        return new NoteToCardQueueReadModel(collectionId, normalizedSourceRelPath, items, summary);
    }

    private static int CountCandidateSlots(IReadOnlyList<CardContext> contexts)
    {
        int count = 0;
        foreach (CardContext context in contexts)
        {
            count += context.Slots.Count;
        }

        return count;
    }

    private static string[] CreateTargetIdArray(IReadOnlyList<CardTarget> targets)
    {
        string[] targetIds = new string[targets.Count];
        for (int index = 0; index < targets.Count; index++)
        {
            targetIds[index] = targets[index].Id;
        }

        return targetIds;
    }

    private static Dictionary<(string ContextId, string SlotId), GeneratedCardContextCardReadModel> BuildGeneratedCardLookup(
        IReadOnlyList<GeneratedCardContextCardReadModel> generatedCards,
        string normalizedSourceRelPath)
    {
        var generatedByKey = new Dictionary<(string ContextId, string SlotId), GeneratedCardContextCardReadModel>();

        foreach (GeneratedCardContextCardReadModel card in generatedCards)
        {
            if (string.IsNullOrWhiteSpace(card.ContextId)
                || string.IsNullOrWhiteSpace(card.SlotId)
                || !string.Equals(
                    CardContextDraftNoteDocument.NormalizeRelPath(card.SourceRelPath),
                    normalizedSourceRelPath,
                    StringComparison.Ordinal))
            {
                continue;
            }

            generatedByKey.TryAdd((card.ContextId, card.SlotId), card);
        }

        return generatedByKey;
    }

    private static Dictionary<string, CardTarget> BuildTargetLookup(IReadOnlyList<CardTarget> targets)
    {
        var targetsById = new Dictionary<string, CardTarget>(targets.Count, StringComparer.Ordinal);

        foreach (CardTarget target in targets)
        {
            targetsById.TryAdd(target.Id, target);
        }

        return targetsById;
    }

    private static Dictionary<string, List<CardTarget>> BuildTargetsBySlotLookup(IReadOnlyList<CardTarget> targets)
    {
        var targetsBySlotId = new Dictionary<string, List<CardTarget>>(StringComparer.Ordinal);

        foreach (CardTarget target in targets)
        {
            if (!targetsBySlotId.TryGetValue(target.SlotId, out List<CardTarget>? slotTargets))
            {
                slotTargets = [];
                targetsBySlotId.Add(target.SlotId, slotTargets);
            }

            slotTargets.Add(target);
        }

        return targetsBySlotId;
    }

    private static Dictionary<string, CardContextImageSurface> BuildImageSurfaceLookup(IReadOnlyList<CardContextImageSurface> images)
    {
        var imagesByBlockId = new Dictionary<string, CardContextImageSurface>(images.Count, StringComparer.Ordinal);

        foreach (CardContextImageSurface image in images)
        {
            imagesByBlockId.TryAdd(image.ImageBlockId, image);
        }

        return imagesByBlockId;
    }

    private static Dictionary<(string ImageBlockId, string MaskId), ImageOcclusionMask> BuildImageMaskLookup(
        IReadOnlyList<CardContextImageSurface> images)
    {
        var masksByImageAndId = new Dictionary<(string ImageBlockId, string MaskId), ImageOcclusionMask>();

        foreach (CardContextImageSurface image in images)
        {
            foreach (ImageOcclusionMask mask in image.Masks)
            {
                masksByImageAndId.TryAdd((image.ImageBlockId, mask.Id), mask);
            }
        }

        return masksByImageAndId;
    }

    private static IReadOnlySet<int> FindDuplicateDisplayIndexes(IReadOnlyList<CardSlot> slots)
    {
        var seen = new HashSet<int>();
        var duplicates = new HashSet<int>();

        foreach (CardSlot slot in slots)
        {
            if (!seen.Add(slot.DisplayClozeIndex))
            {
                duplicates.Add(slot.DisplayClozeIndex);
            }
        }

        return duplicates;
    }
}
