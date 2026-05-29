using System.Text;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static partial class FlashcardClozeText
{
    private const string HiddenClozeText = "[[...]]";

    internal static string Render(string text, int targetIndex, bool revealTarget)
    {
        IReadOnlyList<ClozeMarker> markers = Parse(text);
        if (markers.Count == 0)
        {
            return text;
        }

        StringBuilder builder = new();
        int position = 0;
        foreach (ClozeMarker marker in markers)
        {
            builder.Append(text, position, marker.Start - position);
            builder.Append(marker.Index == targetIndex && !revealTarget ? HiddenClozeText : marker.Text);
            position = marker.Start + marker.Length;
        }

        builder.Append(text, position, text.Length - position);
        return builder.ToString();
    }
}
