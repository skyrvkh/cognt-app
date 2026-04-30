using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static PendingNoteCreation BuildCreatedNoteDraft(
        string root,
        string title,
        string initialContent)
    {
        return new PendingNoteCreation(
            CreateUniqueMetadataNoteRelPath(root),
            title,
            initialContent);
    }

    private PendingNoteCreation BuildExtractNoteDraft(
        string root,
        string sourceRelativePath,
        string initialContent)
    {
        string fullSourcePath = ResolveExistingRelativeNotePathInRoot(root, sourceRelativePath);
        string fileRelPath = CreateUniqueExtractNoteRelPath(root, fullSourcePath);
        string extractTitle = CreateExtractTitleFromRelPath(fileRelPath);
        string content = IsBlockSuiteDocumentContent(initialContent)
            ? ApplyBlockSuiteDocumentTitle(initialContent, extractTitle)
            : BlockSuiteDocumentContent.FromPlainText(initialContent, extractTitle);

        return new PendingNoteCreation(fileRelPath, extractTitle, content);
    }

    private sealed class PendingNoteCreation
    {
        internal PendingNoteCreation(string relativePath, string title, string content)
        {
            RelativePath = relativePath;
            Title = title;
            Content = content;
        }

        internal string RelativePath { get; }

        internal string Title { get; }

        internal string Content { get; }
    }
}
