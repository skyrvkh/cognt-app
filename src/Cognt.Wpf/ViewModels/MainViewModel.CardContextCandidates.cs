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

        CardCandidateReadModel[] candidates = CurrentNoteToCardQueue.Items
            .Where(CanGenerateCandidate)
            .ToArray();

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
            CardContext context = document.Contexts.FirstOrDefault(item =>
                    string.Equals(item.Id, selected.ContextId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("The selected card context no longer exists.");

            context = CardContextAuthoring.SetCardSlotDisabled(context, selected.SlotId, isDisabled: true);
            PersistCardContextDraftDocument(relativePath, noteContent, ReplaceDraftContext(document, context));
            ReloadCurrentNoteToCardQueue(relativePath, noteContent);

            SelectedCardCandidate = CurrentNoteToCardQueue?.Items.FirstOrDefault(item =>
                    string.Equals(item.ContextId, selected.ContextId, StringComparison.Ordinal)
                    && string.Equals(item.SlotId, selected.SlotId, StringComparison.Ordinal));
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
            Dictionary<string, HashSet<string>> slotIdsByContextId = candidates
                .GroupBy(candidate => candidate.ContextId, StringComparer.Ordinal)
                .ToDictionary(
                    grouping => grouping.Key,
                    grouping => grouping.Select(candidate => candidate.SlotId).ToHashSet(StringComparer.Ordinal),
                    StringComparer.Ordinal);

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
}
