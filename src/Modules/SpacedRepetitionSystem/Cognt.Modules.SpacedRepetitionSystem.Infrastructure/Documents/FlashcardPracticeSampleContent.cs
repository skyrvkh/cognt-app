namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

public sealed record FlashcardPracticeSample(string FrontContent, string BackContent);

public static class FlashcardPracticeSampleContent
{
    public static IReadOnlyList<FlashcardPracticeSample> Create()
    {
        return
        [
            CreateCard(
                "What organelle produces most ATP in a eukaryotic cell?",
                "Mitochondria produce most ATP through cellular respiration."),
            CreateCard(
                "What molecule carries genetic instructions in cells?",
                "DNA carries genetic instructions used for growth, function, and reproduction."),
            CreateCard(
                "What does a pH below 7 indicate?",
                "A pH below 7 indicates an acidic solution."),
            CreateCard(
                "Which trade route connected East Asia with Europe?",
                "The Silk Road connected East Asia with Europe through overland trade networks."),
            CreateCard(
                "What does immutability mean?",
                "An immutable value cannot be changed after it is created.")
        ];
    }

    private static FlashcardPracticeSample CreateCard(string front, string back)
    {
        string document = FlashcardCardContentCodec.CreateBasicDocument(
            BlockSuiteDocumentText.FromPlainText(front, "Front"),
            BlockSuiteDocumentText.FromPlainText(back, "Back"),
            []);
        MaterializedFlashcardContent materialized = FlashcardCardContentCodec.MaterializeSingleFile(document);
        return new FlashcardPracticeSample(materialized.FrontContent, materialized.BackContent);
    }
}
