using System.IO;
using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private void TryRefreshSavedNoteMentionIndex(string notePath, string content)
    {
        string? root = treeRepositoryRoot;
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        string normalizedRoot = NormalizeRoot(root);
        string fullPath = Path.GetFullPath(notePath);
        if (!IsPathInsideRoot(fullPath, normalizedRoot) || !IsNoteFilePath(fullPath))
        {
            return;
        }

        string relativePath = NormalizeRelativePath(Path.GetRelativePath(normalizedRoot, fullPath));
        ITreeRepository repository = OpenTreeRepositoryForCollectionPath(normalizedRoot);
        Dictionary<string, DocumentLinkNote> notesByPath = LoadDocumentLinkNotesByRelativePath(normalizedRoot, repository);

        if (notesByPath.TryGetValue(relativePath, out DocumentLinkNote? sourceNote))
        {
            ReplaceSourceDocumentMentions(normalizedRoot, repository, sourceNote, content, notesByPath);
        }
    }

    private static void EnsureDocumentMentionIndexCurrent(
        string root,
        ITreeRepository repository,
        DocumentLinkNote currentNote,
        string currentContent,
        IReadOnlyDictionary<string, DocumentLinkNote> notesByPath)
    {
        if (!repository.HasDocumentMentions())
        {
            RebuildDocumentMentionIndex(root, repository, notesByPath);
        }

        ReplaceSourceDocumentMentions(root, repository, currentNote, currentContent, notesByPath);
    }
}
