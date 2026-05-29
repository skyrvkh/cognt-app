namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextQueueBuilder
{
    private static IReadOnlyList<TextAnchorPreviewReadModel> BuildTextAnchorPreviews(IReadOnlyList<CardTarget> targets)
    {
        var previews = new List<TextAnchorPreviewReadModel>();

        foreach (CardTarget target in targets)
        {
            foreach (TextAnchor anchor in target.TextAnchors)
            {
                previews.Add(new TextAnchorPreviewReadModel(target.Id, anchor.Id, anchor.BlockId, anchor.MarkId));
            }
        }

        return previews;
    }

    private static IReadOnlyList<ImageMaskPreviewReadModel> BuildImageAnchorPreviews(
        IReadOnlyDictionary<string, CardContextImageSurface> imagesByBlockId,
        IReadOnlyDictionary<(string ImageBlockId, string MaskId), ImageOcclusionMask> masksByImageAndId,
        IReadOnlyList<CardTarget> targets)
    {
        var previews = new List<ImageMaskPreviewReadModel>();

        foreach (CardTarget target in targets)
        {
            foreach (ImageMaskAnchor anchor in target.ImageAnchors)
            {
                imagesByBlockId.TryGetValue(anchor.ImageBlockId, out CardContextImageSurface? image);
                masksByImageAndId.TryGetValue((anchor.ImageBlockId, anchor.MaskId), out ImageOcclusionMask? mask);

                previews.Add(new ImageMaskPreviewReadModel(
                    target.Id,
                    anchor.Id,
                    anchor.ImageBlockId,
                    anchor.MaskId,
                    image?.ImageRelPath,
                    mask?.Label));
            }
        }

        return previews;
    }

    private static string BuildAnswerPreview(IReadOnlyList<CardTarget> targets)
    {
        var answers = new List<string>();
        var seenAnswers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (CardTarget target in targets)
        {
            if (string.IsNullOrWhiteSpace(target.Answer)
                || !seenAnswers.Add(target.Answer))
            {
                continue;
            }

            answers.Add(target.Answer);
        }

        return string.Join(", ", answers);
    }
}
