using System.IO;
using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private sealed partial class DocumentMentionBuilder
    {
        private readonly string root;
        private readonly DocumentLinkNote source;
        private readonly string sourceBlockId;
        private readonly IReadOnlyDictionary<string, DocumentLinkNote> notesByPath;
        private readonly StringBuilder text = new();
        private readonly List<DocumentMentionOccurrence> occurrences = [];

        internal DocumentMentionBuilder(
            string root,
            DocumentLinkNote source,
            string sourceBlockId,
            IReadOnlyDictionary<string, DocumentLinkNote> notesByPath)
        {
            this.root = root;
            this.source = source;
            this.sourceBlockId = string.IsNullOrWhiteSpace(sourceBlockId)
                ? DocumentMentionBlockId
                : sourceBlockId.Trim();
            this.notesByPath = notesByPath;
        }

        internal string Text => text.ToString();

        internal void AppendText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            int start = text.Length;
            text.Append(value);
            AddInlineReferenceMentionsFromText(value, start);
            text.Append(' ');
        }

        internal void AppendReference(string target, string? title, string mentionType)
        {
            string displayText = string.IsNullOrWhiteSpace(title)
                ? Path.GetFileNameWithoutExtension(NormalizeDocumentTarget(target) ?? target)
                : title.Trim();
            int start = text.Length;
            text.Append(displayText);
            AddDocumentReferenceMention(target, start, Math.Max(1, displayText.Length), displayText, mentionType);
            text.Append(' ');
        }

        internal void AppendReferenceOrText(
            string? target,
            string? title,
            string mentionType,
            string? fallbackText)
        {
            if (!string.IsNullOrWhiteSpace(target))
            {
                AppendReference(target, title, mentionType);
                return;
            }

            if (!string.IsNullOrEmpty(fallbackText))
            {
                AppendText(fallbackText);
            }
        }

        internal void AddTitleMention(DocumentLinkNote target, int start, int length, string matchedText)
        {
            AddMentionOccurrence(
                target.DocumentId,
                DocumentTitleMentionType,
                matchedText,
                start,
                length,
                ResolvedMentionStatus,
                skipWhenOverlapping: true);
        }

        internal void AddAmbiguousTitleMention(int start, int length, string matchedText)
        {
            AddMentionOccurrence(
                targetDocumentId: null,
                DocumentTitleMentionType,
                matchedText,
                start,
                length,
                AmbiguousMentionStatus,
                skipWhenOverlapping: true);
        }
    }
}
