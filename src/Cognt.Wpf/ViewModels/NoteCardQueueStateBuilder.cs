using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.ViewModels;

internal sealed record NoteCardQueueState(
    Dictionary<(string ContextId, string SlotId), CardCandidateReadModel> Lookup,
    IReadOnlyList<CardCandidateReadModel> VisibleCandidates);

internal static class NoteCardQueueStateBuilder
{
    internal static NoteCardQueueState Create(NoteToCardQueueReadModel? queue)
    {
        if (queue is null || queue.Items.Count == 0)
        {
            return new NoteCardQueueState([], Array.Empty<CardCandidateReadModel>());
        }

        Dictionary<(string ContextId, string SlotId), CardCandidateReadModel> lookup = new(queue.Items.Count);
        List<CardCandidateReadModel>? visibleCandidates = null;

        for (int index = 0; index < queue.Items.Count; index++)
        {
            CardCandidateReadModel candidate = queue.Items[index];
            lookup[(candidate.ContextId, candidate.SlotId)] = candidate;

            if (candidate.Status == CardCandidateStatus.Disabled)
            {
                visibleCandidates ??= CopyCandidatesBeforeDisabledItem(queue.Items, index);
                continue;
            }

            visibleCandidates?.Add(candidate);
        }

        return new NoteCardQueueState(lookup, visibleCandidates ?? queue.Items);
    }

    private static List<CardCandidateReadModel> CopyCandidatesBeforeDisabledItem(
        IReadOnlyList<CardCandidateReadModel> candidates,
        int disabledItemIndex)
    {
        var visibleCandidates = new List<CardCandidateReadModel>(candidates.Count - 1);

        for (int index = 0; index < disabledItemIndex; index++)
        {
            visibleCandidates.Add(candidates[index]);
        }

        return visibleCandidates;
    }
}
