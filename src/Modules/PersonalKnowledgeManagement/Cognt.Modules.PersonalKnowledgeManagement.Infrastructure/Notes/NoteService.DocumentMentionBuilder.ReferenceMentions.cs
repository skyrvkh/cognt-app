using System.Text.RegularExpressions;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private sealed partial class DocumentMentionBuilder
    {
        private void AddInlineReferenceMentionsFromText(string value, int offset)
        {
            foreach (Match match in RawNoteMentionRegex.Matches(value))
            {
                string target = match.Groups[1].Value.Split('|')[0];
                AddDocumentReferenceMention(
                    target,
                    offset + match.Index,
                    match.Length,
                    match.Value,
                    ManualReferenceMentionType);
            }

            foreach (Match match in MarkdownLinkRegex.Matches(value))
            {
                AddDocumentReferenceMention(
                    match.Groups[1].Value,
                    offset + match.Index,
                    match.Length,
                    match.Value,
                    ExplicitLinkMentionType);
            }
        }

        private void AddDocumentReferenceMention(
            string target,
            int start,
            int length,
            string matchedText,
            string mentionType)
        {
            string? normalizedTarget = NormalizeDocumentTarget(target);
            if (string.IsNullOrWhiteSpace(normalizedTarget) || !IsNoteRelativePath(normalizedTarget))
            {
                return;
            }

            string? resolvedPath = ResolveKnownDocumentPath(root, source.RelativePath, normalizedTarget, notesByPath);
            string? targetDocumentId = resolvedPath is not null
                && notesByPath.TryGetValue(resolvedPath, out DocumentLinkNote? targetNote)
                    ? targetNote.DocumentId
                    : null;
            string status = targetDocumentId is null ? UnresolvedMentionStatus : ResolvedMentionStatus;

            AddMentionOccurrence(
                targetDocumentId,
                mentionType,
                matchedText,
                start,
                length,
                status,
                skipWhenOverlapping: false);
        }
    }
}
