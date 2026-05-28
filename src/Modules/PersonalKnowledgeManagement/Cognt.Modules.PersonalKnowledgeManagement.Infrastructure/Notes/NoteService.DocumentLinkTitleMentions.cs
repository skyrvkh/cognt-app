using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static IReadOnlyList<DocumentMentionRow> MaterializeDocumentMentionRows(
        DocumentMentionBuilder builder,
        DocumentLinkNote source,
        DocumentLinkTitleIndex titleIndex,
        long now,
        ILogger<NoteService>? logger = null)
    {
        ApplyTitleMentions(builder, source, titleIndex, logger);
        return builder.MaterializeRows(now);
    }

    private static void ApplyTitleMentions(
        DocumentMentionBuilder builder,
        DocumentLinkNote source,
        DocumentLinkTitleIndex titleIndex,
        ILogger<NoteService>? logger = null)
    {
        string plainText = builder.Text;
        foreach (DocumentLinkNote note in titleIndex.UnambiguousTitles.Values)
        {
            if (string.Equals(note.DocumentId, source.DocumentId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (DocumentLinkTitleMatch occurrence in EnumerateTitleMentionMatches(plainText, note, logger))
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
            foreach (DocumentLinkTitleMatch occurrence in EnumerateTitleMentionMatches(plainText, representative, logger))
            {
                builder.AddAmbiguousTitleMention(
                    occurrence.MatchStart,
                    occurrence.MatchLength,
                    occurrence.MatchedText);
            }
        }
    }
}
