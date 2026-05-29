namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardClozeText
{
    internal static IReadOnlyList<int> GetIndexes(string text)
    {
        return Parse(text)
            .Select(marker => marker.Index)
            .Distinct()
            .Order()
            .ToArray();
    }

    internal static IReadOnlyList<ClozeMarker> GetMarkers(string text)
    {
        return Parse(text);
    }
}
