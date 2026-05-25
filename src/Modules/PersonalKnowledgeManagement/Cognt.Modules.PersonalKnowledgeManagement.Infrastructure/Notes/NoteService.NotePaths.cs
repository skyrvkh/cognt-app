using System.IO;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static string ResolveExistingRelativeNotePathInRoot(string root, string relativePath)
    {
        return ResolveExistingPathInRoot(
            root,
            relativePath,
            "Note relative path is required.",
            "Note paths must be relative to the open collection root.",
            "Note files must be inside the open collection root.",
            "The note file does not exist.",
            EnsureNotePath);
    }

    private static string ResolveExistingNotePathInRoot(
        string root,
        string relativePath,
        string missingRelativePathMessage,
        string rootedPathMessage,
        string outsideRootMessage,
        string missingFileMessage)
    {
        return ResolveExistingPathInRoot(
            root,
            relativePath,
            missingRelativePathMessage,
            rootedPathMessage,
            outsideRootMessage,
            missingFileMessage,
            EnsureNotePath);
    }

    private static string ResolveExistingMentionablePathInRoot(
        string root,
        string relativePath,
        string missingRelativePathMessage,
        string rootedPathMessage,
        string outsideRootMessage,
        string missingFileMessage)
    {
        return ResolveExistingPathInRoot(
            root,
            relativePath,
            missingRelativePathMessage,
            rootedPathMessage,
            outsideRootMessage,
            missingFileMessage,
            EnsureMentionablePath);
    }

    private static string ResolveExistingPathInRoot(
        string root,
        string relativePath,
        string missingRelativePathMessage,
        string rootedPathMessage,
        string outsideRootMessage,
        string missingFileMessage,
        Action<string> validatePath)
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

        validatePath(fullPath);

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
                validatePath(aliasFullPath);
                return aliasFullPath;
            }
        }

        throw new FileNotFoundException(missingFileMessage, fullPath);
    }
}
