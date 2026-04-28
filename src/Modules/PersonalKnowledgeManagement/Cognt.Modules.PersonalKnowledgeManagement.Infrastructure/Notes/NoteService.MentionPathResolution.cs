using System.IO;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private string ResolveMentionPathInCollectionRoot(string root, string relativePath)
    {
        string normalizedRelativePath = NormalizeMentionLookupPath(relativePath);

        try
        {
            return ResolveExistingNotePathInRoot(
                root,
                normalizedRelativePath,
                "Mention path is required.",
                "Mention paths must be relative to the open collection root.",
                "Mention path is outside the open collection root.",
                "The mentioned note file does not exist.");
        }
        catch (FileNotFoundException) when (TryResolveRelocatedMentionPath(root, normalizedRelativePath) is { } resolvedPath)
        {
            return resolvedPath;
        }
    }

    private static string NormalizeMentionLookupPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return relativePath;
        }

        string target = NormalizeDocumentTarget(relativePath) ?? NormalizeRelativePath(relativePath.Trim());

        return target.StartsWith("//", StringComparison.Ordinal)
            ? target
            : target.TrimStart('/');
    }
}
