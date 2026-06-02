namespace CogntApp.ViewModels;

internal static partial class NoteOutlineParser
{
    internal static IReadOnlyList<NoteOutlineItemViewModel> Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        return TryParseBlockSuiteOutline(content, out IReadOnlyList<NoteOutlineItemViewModel> blockSuiteOutline)
            ? blockSuiteOutline
            : ParseMarkdownOutline(content);
    }
}
