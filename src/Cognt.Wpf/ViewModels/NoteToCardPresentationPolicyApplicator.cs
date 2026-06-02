using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.ViewModels;

internal sealed record NoteToCardPresentationPolicyApplication(
    CardContextDraftNoteDocument Document,
    bool HasChanges);

internal static partial class NoteToCardPresentationPolicyApplicator
{
    internal static NoteToCardPresentationPolicyApplication Apply(
        CardContextDraftNoteDocument document,
        IReadOnlyList<CardCandidateReadModel> targets,
        CardCandidateReadModel? selected,
        CardPresentationPolicy policy)
    {
        HashSet<string> targetContextIds = targets
            .Select(candidate => candidate.ContextId)
            .ToHashSet(StringComparer.Ordinal);

        CardContext[] updatedContexts = document.Contexts
            .Select(context => ApplyToContext(context, targetContextIds, selected, policy))
            .ToArray();

        if (document.Contexts.SequenceEqual(updatedContexts))
        {
            return new(document, HasChanges: false);
        }

        return new(document with { Contexts = updatedContexts }, HasChanges: true);
    }

    private static CardContext ApplyToContext(
        CardContext context,
        IReadOnlySet<string> targetContextIds,
        CardCandidateReadModel? selected,
        CardPresentationPolicy policy)
    {
        if (!targetContextIds.Contains(context.Id))
        {
            return context;
        }

        if (selected is not null)
        {
            return ApplyToSelectedSlot(context, selected, policy);
        }

        return ApplyToWholeContext(context, policy);
    }

}
