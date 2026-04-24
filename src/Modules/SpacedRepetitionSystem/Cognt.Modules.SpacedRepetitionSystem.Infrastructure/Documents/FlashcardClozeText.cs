using System.Text;
using System.Text.RegularExpressions;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal static class FlashcardClozeText
{
    private const string HiddenClozeText = "[[...]]";
    private const string ExplicitClozeIndexGroupName = "explicit";
    private const string ExplicitClozeTextGroupName = "explicitText";
    private const string SimpleClozeTextGroupName = "simpleText";

    private static readonly Regex ClozeRegex = new(
        @"\{\{(?:(?:c(?<explicit>\d+)::?)(?<explicitText>.*?)(?:::.*?)?|(?<simpleText>[^{}]+?))\}\}",
        RegexOptions.Compiled);

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

    private static IReadOnlyList<ClozeMarker> Parse(string text)
    {
        MatchCollection matches = ClozeRegex.Matches(text);
        List<ClozeMarker> markers = new(matches.Count);
        int nextSimpleIndex = 1;
        foreach (Match match in matches)
        {
            int index = match.Groups[ExplicitClozeIndexGroupName].Success
                ? int.Parse(match.Groups[ExplicitClozeIndexGroupName].Value)
                : nextSimpleIndex++;
            string clozeText = match.Groups[ExplicitClozeTextGroupName].Success
                ? match.Groups[ExplicitClozeTextGroupName].Value
                : match.Groups[SimpleClozeTextGroupName].Value;

            markers.Add(new ClozeMarker(index, clozeText, match.Index, match.Length));
        }

        return markers;
    }

    internal sealed class ClozeMarker
    {
        internal ClozeMarker(int index, string text, int start, int length)
        {
            Index = index;
            Text = text;
            Start = start;
            Length = length;
        }

        internal int Index { get; }

        internal string Text { get; }

        internal int Start { get; }

        internal int Length { get; }
    }
}
