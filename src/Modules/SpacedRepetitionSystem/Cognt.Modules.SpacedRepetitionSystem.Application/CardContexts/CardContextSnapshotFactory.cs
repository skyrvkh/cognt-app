namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextSnapshotFactory
{
    public static CardContextSnapshot CreateSnapshot(
        string sourceRelPath,
        string noteContent,
        CardContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelPath);
        ArgumentNullException.ThrowIfNull(context);

        CardPresentationPolicy defaultPresentationPolicy = ResolvePresentationPolicy(context);
        string contextContent = ExtractSelectedContextContent(noteContent, context);
        string snapshotHash = ComputeSnapshotHash(contextContent, context);

        return new CardContextSnapshot(
            context.Id,
            CardContextDraftNoteDocument.NormalizeRelPath(sourceRelPath),
            contextContent,
            context.Slots,
            context.Targets,
            context.Images,
            defaultPresentationPolicy,
            ResolveStatusFromSnapshotHash(context, snapshotHash),
            snapshotHash);
    }

    public static CardContextDraftNoteDocument RefreshContextStatuses(
        string sourceRelPath,
        string noteContent,
        CardContextDraftNoteDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        CardContext[] contexts = document.Contexts
            .Select(context =>
            {
                if (context.Status == CardContextStatus.Disabled)
                {
                    return context;
                }

                CardContextSnapshot snapshot = CreateSnapshot(sourceRelPath, noteContent, context);
                return context with { Status = snapshot.Status };
            })
            .ToArray();

        return document with
        {
            SourceRelPath = CardContextDraftNoteDocument.NormalizeRelPath(sourceRelPath),
            Contexts = contexts,
        };
    }

    public static string CreateSlotSnapshotHash(
        CardContextSnapshot snapshot,
        string slotId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);

        return ComputeSlotSnapshotHash(snapshot, slotId);
    }

    private static CardPresentationPolicy ResolvePresentationPolicy(CardContext context)
    {
        return context.DefaultPresentationPolicy
            ?? new CardPresentationPolicy(
                CardPresentationPreset.TextHideAllCueCurrent,
                TextClozeFrontMode.HideAllCueCurrent,
                ImageOcclusionFrontMode.None,
                CardBackRevealMode.RevealAllTargets,
                CueCurrentTarget: true,
                RevealSiblingAnswersOnBack: true);
    }

    private static CardPresentationPolicy ResolvePresentationPolicy(
        CardContextSnapshot snapshot,
        CardSlot slot)
    {
        return slot.PresentationOverride ?? snapshot.DefaultPresentationPolicy;
    }

    private static CardContextStatus ResolveStatusFromSnapshotHash(CardContext context, string snapshotHash)
    {
        if (context.Status == CardContextStatus.Disabled)
        {
            return CardContextStatus.Disabled;
        }

        if (string.IsNullOrWhiteSpace(context.LastGeneratedSnapshotHash))
        {
            return context.Status == CardContextStatus.Generated
                ? CardContextStatus.Ready
                : context.Status;
        }

        return string.Equals(context.LastGeneratedSnapshotHash, snapshotHash, StringComparison.Ordinal)
            ? context.Status
            : CardContextStatus.Outdated;
    }
}
