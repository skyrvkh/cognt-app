namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextSnapshotFactory
{
    private static string ExtractSelectedContextContent(string noteContent, CardContext context)
    {
        return CardContextSelectedContentExtractor.Extract(noteContent, context);
    }
}
