using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardCardContentCodec
{
    internal static MaterializedFlashcardContent MaterializeSingleFile(
        string content,
        string? collectionRoot = null)
    {
        if (!TryReadDocument(content, out FlashcardCardDocument? document))
        {
            return MaterializeLegacy(content);
        }

        if (string.Equals(document.CardType, MultimodalClozeCardType, StringComparison.OrdinalIgnoreCase))
        {
            return MaterializeMultimodal(document, collectionRoot);
        }

        if (string.Equals(document.CardType, ClozeCardType, StringComparison.OrdinalIgnoreCase)
            && document.ClozeIndex is int clozeIndex)
        {
            return MaterializeCloze(document, clozeIndex);
        }

        return MaterializeBasic(document);
    }

    private static MaterializedFlashcardContent MaterializeLegacy(string content)
    {
        string text = BlockSuiteDocumentText.ToPlainText(content);

        return new MaterializedFlashcardContent(
            content,
            content,
            text,
            text,
            content,
            [],
            FlashcardContentKind.LegacyText,
            FlashcardContentKind.LegacyText,
            FlashcardMaterializationStatus.Ok);
    }

    private static MaterializedFlashcardContent MaterializeBasic(FlashcardCardDocument document)
    {
        string frontContent = document.FrontContent ?? string.Empty;
        string frontPlainText = BlockSuiteDocumentText.ToPlainText(frontContent);
        string backContent = string.IsNullOrWhiteSpace(document.BackContent)
            ? frontContent
            : document.BackContent;
        string backPlainText = BlockSuiteDocumentText.ToPlainText(backContent);

        return new MaterializedFlashcardContent(
            frontContent,
            backContent,
            frontPlainText,
            backPlainText,
            frontContent,
            NormalizeTags(document.Tags),
            FlashcardContentKind.BlockSuite,
            FlashcardContentKind.BlockSuite,
            FlashcardMaterializationStatus.Ok);
    }

    private static MaterializedFlashcardContent MaterializeCloze(
        FlashcardCardDocument document,
        int clozeIndex)
    {
        string frontContent = document.FrontContent ?? string.Empty;
        string sourceText = BlockSuiteDocumentText.ToPlainText(frontContent);
        string frontText = FlashcardClozeText.Render(sourceText, clozeIndex, revealTarget: false);
        string backText = FlashcardClozeText.Render(sourceText, clozeIndex, revealTarget: true);
        string title = $"Cloze {clozeIndex}";

        return new MaterializedFlashcardContent(
            BlockSuiteDocumentText.FromPlainText(frontText, title),
            BlockSuiteDocumentText.FromPlainText(backText, title),
            frontText,
            backText,
            frontContent,
            NormalizeTags(document.Tags),
            FlashcardContentKind.BlockSuite,
            FlashcardContentKind.BlockSuite,
            FlashcardMaterializationStatus.Ok);
    }
}
