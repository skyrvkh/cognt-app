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

        Dictionary<string, DocumentLinkNote> notesByDocumentId = CreateNotesByDocumentId(notesByPath);

        List<KnowledgeDocumentLinkReadModel> incoming = MaterializeDocumentLinkReadModels(
            "incoming",
            repository.LoadDocumentMentionsByTarget(currentNote.DocumentId),
            notesByDocumentId);

        List<KnowledgeDocumentLinkReadModel> outgoing = MaterializeDocumentLinkReadModels(
            "outgoing",
            FilterSelfDocumentMentions(repository.LoadDocumentMentionsBySource(currentNote.DocumentId), currentNote.DocumentId),
            notesByDocumentId);

        return new KnowledgeDocumentLinksReadModel(
            OrderDocumentLinks(incoming, DocumentLinkSortDirection.Incoming),
            OrderDocumentLinks(outgoing, DocumentLinkSortDirection.Outgoing));
    }

    private static IReadOnlyList<DocumentMentionRow> FilterSelfDocumentMentions(
        IReadOnlyList<DocumentMentionRow> mentions,
        string currentDocumentId)
    {
        List<DocumentMentionRow> filteredMentions = new(mentions.Count);
        foreach (DocumentMentionRow mention in mentions)
        {
            if (!string.Equals(mention.TargetDocumentId, currentDocumentId, StringComparison.OrdinalIgnoreCase))
            {
                filteredMentions.Add(mention);
            }
        }

        return filteredMentions;
    }

    private static KnowledgeDocumentLinkReadModel[] OrderDocumentLinks(
        IReadOnlyList<KnowledgeDocumentLinkReadModel> links,
        DocumentLinkSortDirection direction)
    {
        if (links.Count == 0)
        {
            return [];
        }

        DocumentLinkSortRow[] rows = new DocumentLinkSortRow[links.Count];
        for (int index = 0; index < rows.Length; index++)
        {
            rows[index] = new DocumentLinkSortRow(links[index], index);
        }

        Array.Sort(rows, (left, right) => CompareDocumentLinks(left, right, direction));

        KnowledgeDocumentLinkReadModel[] orderedLinks = new KnowledgeDocumentLinkReadModel[rows.Length];
        for (int index = 0; index < orderedLinks.Length; index++)
        {
            orderedLinks[index] = rows[index].Link;
        }

        return orderedLinks;
    }

    private static int CompareDocumentLinks(
        DocumentLinkSortRow left,
        DocumentLinkSortRow right,
        DocumentLinkSortDirection direction)
    {
        int comparison = direction == DocumentLinkSortDirection.Incoming
            ? (right.Link.UpdatedAt ?? 0).CompareTo(left.Link.UpdatedAt ?? 0)
            : string.Compare(left.Link.DisplayTitle, right.Link.DisplayTitle, StringComparison.OrdinalIgnoreCase);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = direction == DocumentLinkSortDirection.Incoming
            ? string.Compare(left.Link.DisplayTitle, right.Link.DisplayTitle, StringComparison.OrdinalIgnoreCase)
            : string.Compare(left.Link.OpenRelativePath, right.Link.OpenRelativePath, StringComparison.OrdinalIgnoreCase);
        return comparison != 0
            ? comparison
            : left.SequenceIndex.CompareTo(right.SequenceIndex);
    }

    private readonly struct DocumentLinkSortRow(KnowledgeDocumentLinkReadModel link, int sequenceIndex)
    {
        internal KnowledgeDocumentLinkReadModel Link { get; } = link;

        internal int SequenceIndex { get; } = sequenceIndex;
    }

    private enum DocumentLinkSortDirection
    {
        Incoming,
        Outgoing
    }
}
