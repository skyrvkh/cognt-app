namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public enum FlashcardContentKind
{
    BlockSuite = 0,
    LegacyText = 1,
    MultimodalCloze = 2,
}

public enum FlashcardMaterializationStatus
{
    Ok = 0,
    CardFileMissing = 1,
    ReferencedMediaMissing = 2,
    InvalidEnvelope = 3,
}

public enum FlashcardSemanticStyle
{
    CurrentTarget = 0,
    SiblingTarget = 1,
    HiddenTarget = 2,
    ResolvedTarget = 3,
    Neutral = 4,
    Disabled = 5,
    Invalid = 6,
}

public enum CardVisualRole
{
    CurrentTarget = 0,
    SiblingTarget = 1,
    HiddenTarget = 2,
    RevealedTarget = 3,
    Neutral = 4,
    Disabled = 5,
    Invalid = 6,
}

public sealed record MultimodalTextSegmentReadModel(
    string Text,
    string? SlotId,
    FlashcardSemanticStyle Style,
    bool IsHidden);

public sealed record ImageMaskReadModel(
    string Id,
    string TargetId,
    ImageMaskShape Shape,
    double X,
    double Y,
    double Width,
    double Height,
    double? Cx,
    double? Cy,
    double? Rx,
    double? Ry,
    IReadOnlyList<NormalizedPoint>? Points,
    string? Label,
    string? SlotId,
    FlashcardSemanticStyle Style,
    bool IsHidden);

public sealed record ImageOcclusionReadModel(
    string ImageRelPath,
    string? ImageFullPath,
    IReadOnlyList<ImageMaskReadModel> Masks);

public sealed record MultimodalClozeReadModel(
    IReadOnlyList<MultimodalTextSegmentReadModel> TextSegments,
    string ContextText,
    ImageOcclusionReadModel? ImageOcclusion,
    CardPresentationPolicy Presentation,
    string CurrentSlotId);
