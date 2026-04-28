using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static IReadOnlyList<DocumentMentionRow> MaterializeDocumentMentionRows(
        DocumentMentionBuilder builder,
        DocumentLinkNote source,
        DocumentLinkTitleIndex titleIndex,
        long now)
    {
        ApplyTitleMentions(builder, source, titleIndex);
        return builder.MaterializeRows(now);
    }

    private static void ApplyTitleMentions(
        DocumentMentionBuilder builder,
        DocumentLinkNote source,
        DocumentLinkTitleIndex titleIndex)
    {
        string plainText = builder.Text;
        foreach (DocumentLinkNote note in titleIndex.UnambiguousTitles.Values)
        {
            if (string.Equals(note.DocumentId, source.DocumentId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (DocumentLinkTitleMatch occurrence in EnumerateTitleMentionMatches(plainText, note))
            {
                builder.AddTitleMention(
                    note,
                    occurrence.MatchStart,
                    occurrence.MatchLength,
                    occurrence.MatchedText);
            }
        }

        foreach (IReadOnlyList<DocumentLinkNote> candidates in titleIndex.AmbiguousTitles.Values)
        {
            if (!candidates.Any(note => !string.Equals(note.DocumentId, source.DocumentId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            DocumentLinkNote representative = candidates[0];
            foreach (DocumentLinkTitleMatch occurrence in EnumerateTitleMentionMatches(plainText, representative))
            {
                builder.AddAmbiguousTitleMention(
                    occurrence.MatchStart,
                    occurrence.MatchLength,
                    occurrence.MatchedText);
            }
        }
    }
}
