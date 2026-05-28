using System.IO;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static string? ResolveKnownDocumentPath(
        string root,
        string sourceRelativePath,
        string rawTarget,
        IReadOnlyDictionary<string, DocumentLinkNote> notesByPath)
    {
        string? normalizedTarget = NormalizeDocumentTarget(rawTarget);
        if (string.IsNullOrWhiteSpace(normalizedTarget) || !IsNoteRelativePath(normalizedTarget))
        {
            return null;
        }

        foreach (string candidate in EnumerateDocumentPathCandidates(sourceRelativePath, normalizedTarget))
        {
            string fullPath = Path.GetFullPath(Path.Combine(root, ToPlatformPath(candidate)));
            if (!IsPathInsideRoot(fullPath, root) || !IsNoteFilePath(fullPath))
            {
                continue;
            }

            string relativePath = NormalizeRelativePath(Path.GetRelativePath(root, fullPath));
            if (notesByPath.ContainsKey(relativePath))
            {
                return relativePath;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateDocumentPathCandidates(
        string sourceRelativePath,
        string normalizedTarget)
    {
        yield return normalizedTarget.TrimStart('/');

        string sourceDirectory = Path.GetDirectoryName(ToPlatformPath(sourceRelativePath)) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(sourceDirectory))
        {
            yield return NormalizeRelativePath(Path.Combine(sourceDirectory, ToPlatformPath(normalizedTarget)));
        }
    }

    private static string? NormalizeDocumentTarget(string rawTarget)
    {
        string target = rawTarget.Trim().Trim('<', '>', '"', '\'').Replace('\\', '/');
        if (target.Length == 0
            || target.StartsWith('#')
            || target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        int titleSeparator = target.IndexOf('|', StringComparison.Ordinal);
        if (titleSeparator >= 0)
        {
            target = target[..titleSeparator].Trim();
        }

        int fragmentIndex = target.IndexOf('#', StringComparison.Ordinal);
        if (fragmentIndex >= 0)
        {
            target = target[..fragmentIndex].Trim();
        }

        int queryIndex = target.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            target = target[..queryIndex].Trim();
        }

        try
        {
            target = Uri.UnescapeDataString(target);
        }
        catch (UriFormatException)
        {
            // Keep the trimmed target when URI decoding fails.
        }

        return string.IsNullOrWhiteSpace(target) ? null : target;
    }

    private static bool IsNoteRelativePath(string relativePath)
    {
        string extension = Path.GetExtension(relativePath);
        return string.Equals(extension, BlockSuiteNoteExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, MarkdownNoteExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, MarkdownImportExtension, StringComparison.OrdinalIgnoreCase);
    }
}
