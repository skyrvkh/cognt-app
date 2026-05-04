using System.Text;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private const string CardContextsDirectoryName = ".cognt-card-contexts";

    public string? ReadCardContextDrafts(string collectionId, string relativePath)
    {
        string root = GetExistingCollectionRoot(collectionId);
        string normalizedRelativePath = NormalizeRelativePath(relativePath);
        ResolveExistingRelativeNotePathInRoot(root, normalizedRelativePath);

        string sidecarPath = GetCardContextDraftSidecarPath(root, normalizedRelativePath);
        return File.Exists(sidecarPath)
            ? File.ReadAllText(sidecarPath, Encoding.UTF8)
            : null;
    }

    public void SaveCardContextDrafts(string collectionId, string relativePath, string? content)
    {
        string root = GetExistingCollectionRoot(collectionId);
        string normalizedRelativePath = NormalizeRelativePath(relativePath);
        ResolveExistingRelativeNotePathInRoot(root, normalizedRelativePath);

        string sidecarPath = GetCardContextDraftSidecarPath(root, normalizedRelativePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            TryDeleteFile(sidecarPath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath)
            ?? throw new InvalidOperationException("The card-context sidecar path is invalid."));
        File.WriteAllText(sidecarPath, EnsureTrailingNewline(content), Encoding.UTF8);
    }

    private static void DeleteCardContextDrafts(string root, IEnumerable<string> noteRelativePaths)
    {
        foreach (string relativePath in noteRelativePaths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Select(NormalizeRelativePath)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            TryDeleteFile(GetCardContextDraftSidecarPath(root, relativePath));
        }
    }
}
