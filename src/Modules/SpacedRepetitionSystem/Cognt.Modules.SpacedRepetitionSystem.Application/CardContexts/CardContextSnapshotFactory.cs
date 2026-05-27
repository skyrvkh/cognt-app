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

        CardPresentationPolicy defaultPresentationPolicy = CardPresentationPolicies.Resolve(context);
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
            CardContextAuthoring.UpdateStatusFromSnapshotHash(context, snapshotHash).Status,
            snapshotHash);
    }

    public static CardContextDraftNoteDocument RefreshContextStatuses(
        string sourceRelPath,
        string noteContent,
        CardContextDraftNoteDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        CardContext[] contexts = new CardContext[document.Contexts.Count];
        for (int index = 0; index < document.Contexts.Count; index++)
        {
            CardContext context = document.Contexts[index];
            if (context.Status == CardContextStatus.Disabled)
            {
                contexts[index] = context;
                continue;
            }

            CardContextSnapshot snapshot = CreateSnapshot(sourceRelPath, noteContent, context);
            contexts[index] = context with { Status = snapshot.Status };
        }

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
}
