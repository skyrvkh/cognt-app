using System.IO;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private async Task GenerateReadyCardCandidatesAsync()
    {
        if (CurrentNoteToCardQueue is null || CurrentNoteToCardQueue.Items.Count == 0)
        {
            RaiseMessage("Note Cards", "There are no card candidates in this note.");
            return;
        }

        IReadOnlyList<CardCandidateReadModel> candidates = CreateGeneratableCandidateList(CurrentNoteToCardQueue.Items);

        await GenerateCardsFromCandidatesAsync(
            candidates,
            "There are no ready or outdated card candidates to generate.");
    }

    private async Task GenerateSelectedCardCandidateAsync()
    {
        if (SelectedCardCandidate is null)
        {
            RaiseMessage("Note Cards", "Select a card candidate first.");
            return;
        }

        if (!CanGenerateCandidate(SelectedCardCandidate))
        {
            RaiseMessage(
                "Note Cards",
                $"The selected candidate is {SelectedCardCandidate.Status} and cannot be generated yet.");
            return;
        }

        await GenerateCardsFromCandidatesAsync(
            [SelectedCardCandidate],
            "The selected card candidate cannot be generated yet.");
    }

    private void RaiseCreateCardContextFromSelectionRequest()
    {
        if (string.IsNullOrWhiteSpace(currentNotePath))
        {
            RaiseMessage("Note Cards", "Open a note before creating a card context.");
            return;
        }

        CreateCardContextFromSelectionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseAddSelectedTextToSelectedCardSlotRequest()
    {
        if (SelectedCardCandidate is null)
        {
            RaiseMessage("Note Cards", "Select a card candidate first.");
            return;
        }

        if (SelectedCardCandidate.Status == CardCandidateStatus.Disabled)
        {
            RaiseMessage("Note Cards", "The selected card candidate is disabled.");
            return;
        }

        AddSelectedTextToSelectedCardSlotRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DisableCurrentlySelectedCardCandidate()
    {
        try
        {
            if (SelectedCardCandidate is null)
            {
                RaiseMessage("Note Cards", "Select a card candidate first.");
                return;
            }

            CardCandidateReadModel selected = SelectedCardCandidate;
            if (selected.Status == CardCandidateStatus.Disabled)
            {
                RaiseMessage("Note Cards", "The selected card candidate is already disabled.");
                return;
            }

            string relativePath = GetRequiredCurrentNoteRelativePath();
            string noteContent = GetCurrentEditorContent?.Invoke() ?? lastSavedContent;
            SaveNoteIfChanged(noteContent);

            CardContextDraftNoteDocument document = ReadCardContextDraftDocument(relativePath);
            CardContext context = ResolveRequestedDraftContext(document, selected.ContextId);

            context = CardContextAuthoring.SetCardSlotDisabled(context, selected.SlotId, isDisabled: true);
            PersistCardContextDraftDocument(relativePath, noteContent, ReplaceDraftContext(document, context));
            ReloadCurrentNoteToCardQueue(relativePath, noteContent);

            SelectedCardCandidate = FindCurrentNoteCardCandidate(selected.ContextId, selected.SlotId);
            RaiseMessage("Note Cards", $"Disabled c{selected.DisplayClozeIndex} {selected.AnswerPreview}.");
        }
        catch (Exception ex)
        {
            RaiseMessage("Disable Card Candidate", ex.Message);
        }
    }

    private async Task GenerateCardsFromCandidatesAsync(
        IReadOnlyList<CardCandidateReadModel> candidates,
        string emptyMessage)
    {
        try
        {
            CommitPendingNoteToCardTags();

            if (string.IsNullOrWhiteSpace(NoteToCardDeckName))
            {
                RaiseMessage("Note Cards", "Deck name is required.");
                return;
            }

            string relativePath = GetRequiredCurrentNoteRelativePath();
            string noteContent = GetCurrentEditorContent?.Invoke() ?? lastSavedContent;
            SaveNoteIfChanged(noteContent);

            if (candidates.Count == 0)
            {
                RaiseMessage("Note Cards", emptyMessage);
                return;
            }

            CardContextDraftNoteDocument document = ReadCardContextDraftDocument(relativePath);
            Dictionary<string, HashSet<string>> slotIdsByContextId = CreateSlotIdsByContextId(candidates);

            int created = 0;
            int updated = 0;
            foreach (CardContext context in document.Contexts)
            {
                if (!slotIdsByContextId.TryGetValue(context.Id, out HashSet<string>? slotIds))
                {
                    continue;
                }

                CardContext filteredContext = FilterContextForGeneration(context, slotIds);
                if (filteredContext.Slots.Count == 0)
                {
                    continue;
                }

                CardContextSnapshot snapshot = CardContextSnapshotFactory.CreateSnapshot(
                    relativePath,
                    noteContent,
                    filteredContext);
                CreateCardsFromCardContextResult result = await createCardsFromCardContextCommand.ExecuteAsync(
                    new CreateCardsFromCardContextRequest(
                        openCollectionPath,
                        NoteToCardDeckName,
                        relativePath,
                        snapshot,
                        NoteToCardTags.ToArray()));
                created += result.CreatedCards;
                updated += result.UpdatedCards;
            }

            ReloadNoteToCardDeckNames();
            ReloadCurrentNoteToCardQueue(relativePath, noteContent);
            RaiseMessage("Note Cards", $"Generated {created} new card(s) and updated {updated} existing card(s).");
        }
        catch (Exception ex)
        {
            RaiseMessage("Generate Cards", ex.Message);
        }
    }

    private static bool CanGenerateCandidate(CardCandidateReadModel candidate)
    {
        return candidate.Status is CardCandidateStatus.Ready or CardCandidateStatus.Outdated;
    }

    private static IReadOnlyList<CardCandidateReadModel> CreateGeneratableCandidateList(
        IReadOnlyList<CardCandidateReadModel> candidates)
    {
        var generatableCandidates = new List<CardCandidateReadModel>();

        foreach (CardCandidateReadModel candidate in candidates)
        {
            if (CanGenerateCandidate(candidate))
            {
                generatableCandidates.Add(candidate);
            }
        }

        return generatableCandidates;
    }

    private static Dictionary<string, HashSet<string>> CreateSlotIdsByContextId(
        IReadOnlyList<CardCandidateReadModel> candidates)
    {
        var slotIdsByContextId = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (CardCandidateReadModel candidate in candidates)
        {
            if (!slotIdsByContextId.TryGetValue(candidate.ContextId, out HashSet<string>? slotIds))
            {
                slotIds = new HashSet<string>(StringComparer.Ordinal);
                slotIdsByContextId.Add(candidate.ContextId, slotIds);
            }

            slotIds.Add(candidate.SlotId);
        }

        return slotIdsByContextId;
    }
}