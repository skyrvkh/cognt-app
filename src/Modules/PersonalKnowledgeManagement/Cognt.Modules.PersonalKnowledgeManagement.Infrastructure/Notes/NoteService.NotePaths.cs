using System.IO;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static string ResolveExistingRelativeNotePathInRoot(string root, string relativePath)
    {
        return ResolveExistingNotePathInRoot(
            root,
            relativePath,
            "Note relative path is required.",
            "Note paths must be relative to the open collection root.",
            "Note files must be inside the open collection root.",
            "The note file does not exist.");
    }

    private static string ResolveExistingNotePathInRoot(
        string root,
        string relativePath,
        string missingRelativePathMessage,
        string rootedPathMessage,
        string outsideRootMessage,
        string missingFileMessage)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException(missingRelativePathMessage, nameof(relativePath));
        }

        string normalizedRelativePath = NormalizeRelativePath(relativePath);
        string platformRelativePath = ToPlatformPath(normalizedRelativePath);
        if (Path.IsPathRooted(platformRelativePath))
        {
            throw new UnauthorizedAccessException(rootedPathMessage);
        }

        string fullPath = Path.GetFullPath(Path.Combine(root, platformRelativePath));
        if (!IsPathInsideRoot(fullPath, root))
        {
            throw new UnauthorizedAccessException(outsideRootMessage);
        }

        EnsureNotePath(fullPath);

        if (File.Exists(fullPath))
        {
            return fullPath;
        }

        if (TryCreateNoteFilesDirectoryAlias(normalizedRelativePath, out string? aliasRelativePath)
            && aliasRelativePath is not null)
        {
            string aliasFullPath = Path.GetFullPath(Path.Combine(root, ToPlatformPath(aliasRelativePath)));
            if (IsPathInsideRoot(aliasFullPath, root) && File.Exists(aliasFullPath))
            {
                EnsureNotePath(aliasFullPath);
                return aliasFullPath;
            }
        }

        throw new FileNotFoundException(missingFileMessage, fullPath);
    }
}
