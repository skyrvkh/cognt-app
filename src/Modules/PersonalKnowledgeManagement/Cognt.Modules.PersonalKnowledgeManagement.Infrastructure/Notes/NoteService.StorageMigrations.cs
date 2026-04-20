using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static void MigrateLegacyFilesDirectory(string root, ITreeRepository repository)
    {
        string legacyDirectory = Path.Combine(root, LegacyFilesDirectoryName);
        string currentDirectory = Path.Combine(root, FilesDirectoryName);
        if (!Directory.Exists(legacyDirectory) || Directory.Exists(currentDirectory))
        {
            return;
        }

        Directory.Move(legacyDirectory, currentDirectory);
        repository.RewriteFileRelPathPrefix(
            $"{LegacyFilesDirectoryName}/",
            $"{FilesDirectoryName}/");
    }
}
