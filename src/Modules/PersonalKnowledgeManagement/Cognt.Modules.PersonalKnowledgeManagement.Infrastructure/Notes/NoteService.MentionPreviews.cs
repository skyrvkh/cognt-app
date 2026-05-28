using System.IO;
using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    string INoteService.ReadMentionPreview(
        string collectionId,
        string relativePath,
        int maxCharacters)
    {
        string normalizedRelativePath = NormalizeRelativePath(relativePath);

        string root = GetExistingCollectionRoot(collectionId);
        string path = ResolveMentionPathInCollectionRoot(root, normalizedRelativePath);
        string resolvedRelativePath = NormalizeRelativePath(Path.GetRelativePath(root, path));
        if (IsPdfPath(path))
        {
            string title = Path.GetFileNameWithoutExtension(path);
            string pdfPreview = BlockSuiteDocumentContent.FromPlainText(
                $"PDF document: {Path.GetFileName(path)}",
                title);
            return pdfPreview;
        }

        string content = File.ReadAllText(path, Encoding.UTF8);
        bool isBlockSuiteNote = IsBlockSuiteNotePath(path);
        string previewTitle = OpenTreeRepositoryForCollectionPath(root).FindNoteByFileRelPath(resolvedRelativePath)?.Title
            ?? (isBlockSuiteNote
                ? Path.GetFileNameWithoutExtension(path)
                : ExtractTitle(content, Path.GetFileName(path)));

        string preview = isBlockSuiteNote
            ? content
            : BlockSuiteDocumentContent.FromPlainText(
                CreateExcerpt(content, Math.Max(0, maxCharacters)),
                previewTitle);
        return preview;
    }

    string INoteService.ResolveMentionPath(
        string collectionId,
        string relativePath)
    {
        string normalizedRelativePath = NormalizeRelativePath(relativePath);
        string root = GetExistingCollectionRoot(collectionId);
        return ResolveMentionPathInCollectionRoot(root, normalizedRelativePath);
    }
}
