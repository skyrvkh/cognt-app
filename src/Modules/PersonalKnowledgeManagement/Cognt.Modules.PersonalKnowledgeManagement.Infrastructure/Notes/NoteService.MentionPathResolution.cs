using System.IO;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private string ResolveMentionPathInCollectionRoot(string root, string relativePath)
    {
        string normalizedRelativePath = NormalizeMentionLookupPath(relativePath);

        try
        {
            return ResolveExistingMentionablePathInRoot(
                root,
                normalizedRelativePath,
                "Mention path is required.",
                "Mention paths must be relative to the open collection root.",
                "Mention path is outside the open collection root.",
                "The mentioned file does not exist.");
        }
        catch (FileNotFoundException ex)
        {
            if (TryResolveRelocatedMentionPath(root, normalizedRelativePath) is { } resolvedPath)
            {
                logger?.LogDebug(
                    ex,
                    "Mention path resolved through relocated-note fallback. Root={Root} RequestedRelativePath={RequestedRelativePath} ResolvedPath={ResolvedPath}",
                    root,
                    normalizedRelativePath,
                    resolvedPath);
                return resolvedPath;
            }

            throw;
        }
    }

    private string NormalizeMentionLookupPath(string relativePath)
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
