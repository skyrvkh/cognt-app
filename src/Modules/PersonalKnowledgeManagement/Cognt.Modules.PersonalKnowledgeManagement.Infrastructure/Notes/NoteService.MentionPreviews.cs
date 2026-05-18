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
        string root = GetExistingCollectionRoot(collectionId);
        string path = ResolveMentionPathInCollectionRoot(root, relativePath);
        string normalizedRelativePath = NormalizeRelativePath(Path.GetRelativePath(root, path));
        string content = File.ReadAllText(path, Encoding.UTF8);
        bool isBlockSuiteNote = IsBlockSuiteNotePath(path);
        string previewTitle = OpenTreeRepositoryForCollectionPath(root).FindNoteByFileRelPath(normalizedRelativePath)?.Title
            ?? (isBlockSuiteNote
                ? Path.GetFileNameWithoutExtension(path)
                : ExtractTitle(content, Path.GetFileName(path)));

        return isBlockSuiteNote
            ? content
            : BlockSuiteDocumentContent.FromPlainText(
                CreateExcerpt(content, Math.Max(0, maxCharacters)),
                previewTitle);
    }

    string INoteService.ResolveMentionPath(
        string collectionId,
        string relativePath)
    {
        string root = GetExistingCollectionRoot(collectionId);
        return ResolveMentionPathInCollectionRoot(root, relativePath);
    }
}
