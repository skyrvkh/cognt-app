using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.Features.NoteToCard.ImageOcclusion;

internal readonly record struct ImageMaskEditorSnapshot(
    CardContext Context,
    string? SelectedMaskId,
    string SelectedMaskLabelText,
    string SelectedMaskAnswerText,
    string SelectedMaskHintText,
    string? SelectedTargetId);
