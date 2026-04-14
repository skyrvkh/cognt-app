using System.Text.Json.Serialization;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public sealed record CardContextDraftNoteDocument(
    string Schema,
    int Version,
    string SourceRelPath,
    IReadOnlyList<CardContext> Contexts)
{
    public const string CurrentSchema = "cognt-card-contexts";
    public const string LegacySchema = "cognt-card-contexts";
    public const int CurrentVersion = 1;

    public static CardContextDraftNoteDocument Empty(string sourceRelPath)
    {
        return new(
            CurrentSchema,
            CurrentVersion,
            NormalizeRelPath(sourceRelPath),
            []);
    }

    internal static string NormalizeRelPath(string sourceRelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelPath);
        return sourceRelPath.Replace('\\', '/').Trim();
    }
}

public sealed record CardContext(
    string Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title,
    IReadOnlyList<string> ContextBlockIds,
    IReadOnlyList<CardSlot> Slots,
    IReadOnlyList<CardTarget> Targets,
    IReadOnlyList<CardContextImageSurface> Images,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CardPresentationPolicy? DefaultPresentationPolicy,
    CardContextStatus Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LastGeneratedSnapshotHash,
    int Order = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CardCueMode? TextCueMode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CardCueMode? ImageCueMode = null);

public sealed record CardSlot(
    string Id,
    int DisplayClozeIndex,
    IReadOnlyList<string> TargetIds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CardPresentationPolicy? PresentationOverride = null,
    bool IsDisabled = false,
    int Order = 0);

public sealed record CardTarget(
    string Id,
    string SlotId,
    string Answer,
    string? Hint,
    IReadOnlyList<TextAnchor> TextAnchors,
    IReadOnlyList<ImageMaskAnchor> ImageAnchors,
    bool IsDisabled = false);

public sealed record TextAnchor(
    string Id,
    string BlockId,
    string? MarkId,
    string SelectedTextHash,
    string? RangeId = null,
    string? SelectedTextPreview = null);

public sealed record ImageMaskAnchor(
    string Id,
    string ImageBlockId,
    string MaskId);

public sealed record CardContextImageSurface(
    string ImageBlockId,
    string ImageRelPath,
    IReadOnlyList<ImageOcclusionMask> Masks);

public sealed record CardContextSnapshot(
    string Id,
    string SourceRelPath,
    string ContextContent,
    IReadOnlyList<CardSlot> Slots,
    IReadOnlyList<CardTarget> Targets,
    IReadOnlyList<CardContextImageSurface> Images,
    CardPresentationPolicy DefaultPresentationPolicy,
    CardContextStatus Status,
    string SnapshotHash);
