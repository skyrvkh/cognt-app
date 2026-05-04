using System.Collections.ObjectModel;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using CogntApp.Features.NoteToCard.ImageOcclusion;

namespace CogntApp.Features.NoteToCard;

public sealed class CardCandidatePreviewViewModel
{
    public CardCandidatePreviewViewModel(
        string sourceRelPath,
        string contextId,
        string slotId,
        int displayClozeIndex,
        string answerPreview,
        CardCandidateStatus status,
        string presentationLabel,
        string? validationMessage,
        string snapshotHash,
        string frontTextPreview,
        string backTextPreview,
        string contextSnapshotPreview,
        IReadOnlyList<string> targetIds,
        IReadOnlyList<TextAnchorPreviewReadModel> textAnchors,
        IReadOnlyList<ImageMaskPreviewReadModel> imageAnchors,
        IReadOnlyList<CardCandidateImagePreviewViewModel> imagePreviews)
    {
        SourceRelPath = sourceRelPath;
        ContextId = contextId;
        SlotId = slotId;
        DisplayClozeIndex = displayClozeIndex;
        AnswerPreview = answerPreview;
        Status = status;
        PresentationLabel = presentationLabel;
        ValidationMessage = validationMessage;
        SnapshotHash = snapshotHash;
        FrontTextPreview = frontTextPreview;
        BackTextPreview = backTextPreview;
        ContextSnapshotPreview = contextSnapshotPreview;
        TargetIds = new ReadOnlyCollection<string>(targetIds.ToArray());
        TextAnchors = new ReadOnlyCollection<TextAnchorPreviewReadModel>(textAnchors.ToArray());
        ImageAnchors = new ReadOnlyCollection<ImageMaskPreviewReadModel>(imageAnchors.ToArray());
        ImagePreviews = new ReadOnlyCollection<CardCandidateImagePreviewViewModel>(imagePreviews.ToArray());
    }

    public string SourceRelPath { get; }

    public string ContextId { get; }

    public string SlotId { get; }

    public int DisplayClozeIndex { get; }

    public string DisplayLabel => $"c{DisplayClozeIndex}";

    public string AnswerPreview { get; }

    public CardCandidateStatus Status { get; }

    public string PresentationLabel { get; }

    public string? ValidationMessage { get; }

    public string SnapshotHash { get; }

    public string FrontTextPreview { get; }

    public string BackTextPreview { get; }

    public string ContextSnapshotPreview { get; }

    public IReadOnlyList<string> TargetIds { get; }

    public IReadOnlyList<TextAnchorPreviewReadModel> TextAnchors { get; }

    public IReadOnlyList<ImageMaskPreviewReadModel> ImageAnchors { get; }

    public IReadOnlyList<CardCandidateImagePreviewViewModel> ImagePreviews { get; }

    public int TextAnchorCount => TextAnchors.Count;

    public int ImageAnchorCount => ImageAnchors.Count;

    public string Metadata =>
        $"{SourceRelPath} | context {ContextId} | slot {SlotId} | hash {SnapshotHash}";
}

public sealed class CardCandidateImagePreviewViewModel
{
    public CardCandidateImagePreviewViewModel(
        string imageBlockId,
        string imageRelPath,
        ImageMaskEditorViewModel? editor,
        string? missingMediaMessage)
    {
        ImageBlockId = imageBlockId;
        ImageRelPath = imageRelPath;
        Editor = editor;
        MissingMediaMessage = missingMediaMessage;
    }

    public string ImageBlockId { get; }

    public string ImageRelPath { get; }

    public ImageMaskEditorViewModel? Editor { get; }

    public string? MissingMediaMessage { get; }
}

public sealed class CardCandidatePreviewRequestedEventArgs : EventArgs
{
    public CardCandidatePreviewRequestedEventArgs(CardCandidatePreviewViewModel preview)
    {
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));
    }

    public CardCandidatePreviewViewModel Preview { get; }
}
