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
        Dictionary<(string ContextId, string SlotId), GeneratedCardContextCardReadModel> generatedByKey = generatedCards
            .Where(card =>
                !string.IsNullOrWhiteSpace(card.ContextId)
                && !string.IsNullOrWhiteSpace(card.SlotId)
                && string.Equals(
                    CardContextDraftNoteDocument.NormalizeRelPath(card.SourceRelPath),
                    normalizedSourceRelPath,
                    StringComparison.Ordinal))
            .GroupBy(card => (card.ContextId, card.SlotId))
            .ToDictionary(grouping => grouping.Key, grouping => grouping.First());

        List<CardCandidateReadModel> items = [];
        foreach (CardContext context in document.Contexts)
        {
            IReadOnlySet<int> duplicateDisplayIndexes = context.Slots
                .GroupBy(slot => slot.DisplayClozeIndex)
                .Where(grouping => grouping.Count() > 1)
                .Select(grouping => grouping.Key)
                .ToHashSet();
            CardContextSnapshot snapshot = CardContextSnapshotFactory.CreateSnapshot(normalizedSourceRelPath, noteContent, context);
            foreach (CardSlot slot in context.Slots
                         .OrderBy(candidate => candidate.DisplayClozeIndex)
                         .ThenBy(candidate => candidate.Id, StringComparer.Ordinal))
            {
                IReadOnlyList<CardTarget> targets = ResolveTargets(slot, context.Targets)
                    .Where(target => !target.IsDisabled)
                    .ToArray();
                string snapshotHash = CardContextSnapshotFactory.CreateSlotSnapshotHash(snapshot, slot.Id);
                CandidateValidation validation = ValidateCandidate(
                    context,
                    slot,
                    targets,
                    availableBlockIds,
                    duplicateDisplayIndexes);
                generatedByKey.TryGetValue((context.Id, slot.Id), out GeneratedCardContextCardReadModel? generated);

                items.Add(new CardCandidateReadModel(
                    context.Id,
                    slot.Id,
                    slot.DisplayClozeIndex,
                    targets.Select(target => target.Id).ToArray(),
                    BuildAnswerPreview(targets),
                    BuildTextAnchorPreviews(targets),
                    BuildImageAnchorPreviews(context, targets),
                    DetermineStatus(context, slot, validation, generated, snapshotHash),
                    validation.Message,
                    snapshotHash));
            }
        }

        NoteToCardQueueSummaryReadModel summary = BuildSummary(items);
        return new NoteToCardQueueReadModel(collectionId, normalizedSourceRelPath, items, summary);
    }
}
