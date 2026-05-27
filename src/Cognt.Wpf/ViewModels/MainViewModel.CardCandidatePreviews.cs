using System.IO;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using CogntApp.Features.NoteToCard;
using CogntApp.Features.NoteToCard.ImageOcclusion;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private void PreviewSelectedCardCandidate()
    {
        try
        {
            if (SelectedCardCandidate is null)
            {
                RaiseMessage("Note Cards", "Select a card candidate first.");
                return;
            }

            CardCandidatePreviewRequested?.Invoke(
                this,
                new CardCandidatePreviewRequestedEventArgs(BuildCardCandidatePreviewModel(SelectedCardCandidate)));
        }
        catch (Exception ex)
        {
            RaiseMessage("Preview Card Candidate", ex.Message);
        }
    }

    private CardCandidatePreviewViewModel BuildCardCandidatePreviewModel(CardCandidateReadModel candidate)
    {
        string relativePath = GetRequiredCurrentNoteRelativePath();
        string noteContent = GetCurrentEditorContent?.Invoke() ?? lastSavedContent;
        CardContextDraftNoteDocument document = ReadCardContextDraftDocument(relativePath);
        CardContext context = ResolveRequestedDraftContext(document, candidate.ContextId);
        CardSlot slot = ResolveRequiredCardSlot(context.Slots, candidate.SlotId);
        CardPresentationPolicy presentation = CardPresentationPolicies.Resolve(context, slot);
        CardContextSnapshot snapshot = CardContextSnapshotFactory.CreateSnapshot(
            relativePath,
            noteContent,
            context);

        return new CardCandidatePreviewViewModel(
            relativePath,
            candidate.ContextId,
            candidate.SlotId,
            candidate.DisplayClozeIndex,
            candidate.AnswerPreview,
            candidate.Status,
            CardPresentationPolicies.GetDisplayLabel(presentation.Preset),
            candidate.ValidationMessage,
            candidate.SnapshotHash,
            CardCandidateTextPreview.CreateFrontText(
                snapshot.ContextContent,
                context.Slots,
                slot.Id,
                presentation),
            CardCandidateTextPreview.CreateBackText(
                snapshot.ContextContent,
                context.Slots,
                slot.Id,
                presentation),
            BuildCandidateContextExcerpt(snapshot.ContextContent),
            candidate.TargetIds,
            candidate.TextAnchors,
            candidate.ImageAnchors,
            BuildCardCandidateImagePreviews(relativePath, context, slot));
    }

    private List<CardCandidateImagePreviewViewModel> BuildCardCandidateImagePreviews(
        string relativePath,
        CardContext context,
        CardSlot slot)
    {
        List<CardCandidateImagePreviewViewModel> imagePreviews = [];
        foreach (CardContextImageSurface image in context.Images)
        {
            if (image.Masks.Count > 0)
            {
                imagePreviews.Add(BuildCardCandidateImagePreview(relativePath, context, slot, image));
            }
        }

        return imagePreviews;
    }

    private CardCandidateImagePreviewViewModel BuildCardCandidateImagePreview(
        string relativePath,
        CardContext context,
        CardSlot slot,
        CardContextImageSurface image)
    {
        string imageFullPath = ResolveCollectionFilePath(openCollectionPath, image.ImageRelPath);
        if (!File.Exists(imageFullPath))
        {
            return new CardCandidateImagePreviewViewModel(
                image.ImageBlockId,
                image.ImageRelPath,
                editor: null,
                missingMediaMessage: "The referenced image file is missing.");
        }

        ImageMaskEditorViewModel previewEditor = new(
            new OpenImageMaskEditorRequest(
                openCollectionPath,
                relativePath,
                image.ImageBlockId,
                image.ImageRelPath,
                imageFullPath,
                context,
                context.Id))
        {
            PreviewSlotId = slot.Id,
        };

        return new CardCandidateImagePreviewViewModel(
            image.ImageBlockId,
            image.ImageRelPath,
            previewEditor,
            missingMediaMessage: null);
    }

    private static string BuildCandidateContextExcerpt(string contextContent)
    {
        if (string.IsNullOrWhiteSpace(contextContent))
        {
            return "No context content was available for this candidate.";
        }

        const int maximumPreviewLength = 4000;
        string normalized = contextContent.Trim();
        return normalized.Length <= maximumPreviewLength
            ? normalized
            : string.Concat(normalized.AsSpan(0, maximumPreviewLength), Environment.NewLine, "...");
    }
}