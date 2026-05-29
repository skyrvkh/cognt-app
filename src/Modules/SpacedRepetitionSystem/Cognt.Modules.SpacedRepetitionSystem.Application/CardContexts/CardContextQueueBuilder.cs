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
            CardContextQueueLookups lookups = CardContextQueueLookups.Create(context);
            CardContextSnapshot snapshot = CardContextSnapshotFactory.CreateSnapshot(normalizedSourceRelPath, noteContent, context);
            foreach (CardSlot slot in context.Slots
                         .OrderBy(candidate => candidate.DisplayClozeIndex)
                         .ThenBy(candidate => candidate.Id, StringComparer.Ordinal))
            {
                IReadOnlyList<CardTarget> targets = lookups.ResolveEnabledTargets(slot);
                string snapshotHash = CardContextSnapshotFactory.CreateSlotSnapshotHash(snapshot, slot.Id);
                CandidateValidation validation = ValidateCandidate(
                    context,
                    slot,
                    targets,
                    availableBlockIds,
                    lookups.DuplicateDisplayIndexes,
                    lookups.ImagesByBlockId,
                    lookups.MasksByImageAndId);
                generatedByKey.TryGetValue((context.Id, slot.Id), out GeneratedCardContextCardReadModel? generated);

                items.Add(new CardCandidateReadModel(
                    context.Id,
                    slot.Id,
                    slot.DisplayClozeIndex,
                    CreateTargetIdArray(targets),
                    BuildAnswerPreview(targets),
                    BuildTextAnchorPreviews(targets),
                    BuildImageAnchorPreviews(lookups.ImagesByBlockId, lookups.MasksByImageAndId, targets),
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

}
