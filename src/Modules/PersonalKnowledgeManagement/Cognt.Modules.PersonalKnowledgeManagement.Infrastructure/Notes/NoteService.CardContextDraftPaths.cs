using System.Security.Cryptography;
using System.Text;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static string GetCardContextDraftSidecarPath(string root, string relativePath)
    {
        string normalizedRelativePath = NormalizeRelativePath(relativePath);
        return Path.Combine(
            root,
            ResolveCardContextSidecarRootDirectoryName(normalizedRelativePath),
            CardContextsDirectoryName,
            CreateCardContextDraftFileName(normalizedRelativePath));
    }

    private static string GetCardContextDraftMigrationPath(
        string root,
        string filesDirectoryName,
        string normalizedRelativePath)
    {
        return Path.Combine(
            root,
            filesDirectoryName,
            CardContextsDirectoryName,
            CreateCardContextDraftFileName(normalizedRelativePath));
    }

    private static string CreateCardContextDraftFileName(string normalizedRelativePath)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedRelativePath));
        return $"{Convert.ToHexString(hash).ToLowerInvariant()}.json";
    }

    private static string ResolveCardContextSidecarRootDirectoryName(string normalizedRelativePath)
    {
        return HasFirstPathSegment(normalizedRelativePath, LegacyFilesDirectoryName)
            ? LegacyFilesDirectoryName
            : FilesDirectoryName;
    }

    private static bool HasFirstPathSegment(string normalizedRelativePath, string segment)
    {
        return normalizedRelativePath.Length == segment.Length
            ? string.Equals(normalizedRelativePath, segment, StringComparison.OrdinalIgnoreCase)
            : normalizedRelativePath.Length > segment.Length
                && normalizedRelativePath[segment.Length] == '/'
                && normalizedRelativePath.StartsWith(segment, StringComparison.OrdinalIgnoreCase);
    }
}
