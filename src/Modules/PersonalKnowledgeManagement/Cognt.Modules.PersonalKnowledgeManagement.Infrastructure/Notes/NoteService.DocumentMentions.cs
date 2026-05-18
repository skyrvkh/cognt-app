using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private const string DocumentMentionBlockId = "document";
    private const string ExplicitLinkMentionType = "explicit_link";
    private const string DocumentTitleMentionType = "document_title_mention";
    private const string ManualReferenceMentionType = "manual_reference";
    private const string ResolvedMentionStatus = "resolved";
    private const string UnresolvedMentionStatus = "unresolved";
    private const string AmbiguousMentionStatus = "ambiguous";

    private static readonly Regex RawNoteMentionRegex = new(@"!?\[\[([^\]\[\r\n]+)\]\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownLinkRegex = new(@"\[[^\]\r\n]+\]\(([^)\r\n]+)\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    KnowledgeDocumentLinksReadModel INoteService.ReadDocumentLinks(
        string collectionId,
        string currentRelativePath,
        string currentContent)
    {
        string root = GetExistingCollectionRoot(collectionId);
        string normalizedCurrentRelativePath = NormalizeRelativePath(currentRelativePath);
        string currentFullPath = ResolveExistingRelativeNotePathInRoot(root, normalizedCurrentRelativePath);
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(root);
        Dictionary<string, DocumentLinkNote> notesByPath = LoadDocumentLinkNotesByRelativePath(root, repository);

        if (!notesByPath.TryGetValue(normalizedCurrentRelativePath, out DocumentLinkNote? currentNote))
        {
            return new KnowledgeDocumentLinksReadModel([], []);
        }

        EnsureDocumentMentionIndexCurrent(
            root,
            repository,
            currentNote,
            string.IsNullOrWhiteSpace(currentContent) ? File.ReadAllText(currentFullPath, Encoding.UTF8) : currentContent,
            notesByPath);

        Dictionary<string, DocumentLinkNote> notesByDocumentId = notesByPath.Values
            .ToDictionary(note => note.DocumentId, note => note, StringComparer.OrdinalIgnoreCase);

        List<KnowledgeDocumentLinkReadModel> incoming = MaterializeDocumentLinkReadModels(
            "incoming",
            repository.LoadDocumentMentionsByTarget(currentNote.DocumentId),
            notesByDocumentId);

        List<KnowledgeDocumentLinkReadModel> outgoing = MaterializeDocumentLinkReadModels(
            "outgoing",
            repository.LoadDocumentMentionsBySource(currentNote.DocumentId)
                .Where(mention => !string.Equals(mention.TargetDocumentId, currentNote.DocumentId, StringComparison.OrdinalIgnoreCase)),
            notesByDocumentId);

        return new KnowledgeDocumentLinksReadModel(
            incoming
                .OrderByDescending(link => link.UpdatedAt ?? 0)
                .ThenBy(link => link.DisplayTitle, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            outgoing
                .OrderBy(link => link.DisplayTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(link => link.OpenRelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }
}
