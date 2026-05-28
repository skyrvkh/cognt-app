using System.IO;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static string CreateUniqueMetadataNoteRelPath(string root)
    {
        string filesDirectory = Path.Combine(root, FilesDirectoryName);
        Directory.CreateDirectory(filesDirectory);

        string fileRelPath;
        string notePath;

        do
        {
            fileRelPath = NormalizeRelativePath(Path.Combine(
                FilesDirectoryName,
                $"{CreateGuidV7FileStem()}{BlockSuiteNoteExtension}"));
            notePath = Path.Combine(root, ToPlatformPath(fileRelPath));
        }
        while (File.Exists(notePath));

        return fileRelPath;
    }

    private static string CreateUniqueExtractNoteRelPath(
        string root,
        string sourceNotePath)
    {
        string filesDirectory = Path.Combine(root, FilesDirectoryName);
        Directory.CreateDirectory(filesDirectory);

        string sourceStem = CreateSourceFileStem(sourceNotePath);
        string fileRelPath;
        string notePath;

        do
        {
            fileRelPath = NormalizeRelativePath(Path.Combine(
                FilesDirectoryName,
                $"{sourceStem}.{CreateShortUniqueIdentifier()}{BlockSuiteNoteExtension}"));
            notePath = Path.Combine(root, ToPlatformPath(fileRelPath));
        }
        while (File.Exists(notePath));

        return fileRelPath;
    }

    private static string CreateExtractTitleFromRelPath(string fileRelPath)
    {
        string stem = Path.GetFileNameWithoutExtension(fileRelPath).Trim();
        int separatorIndex = stem.LastIndexOf('.');

        return separatorIndex >= 0 && separatorIndex < stem.Length - 1
            ? stem[(separatorIndex + 1)..]
            : stem;
    }

    private static string CreateSourceFileStem(string sourceNotePath)
    {
        string stem = Path.GetFileNameWithoutExtension(sourceNotePath).Trim();
        if (Guid.TryParse(stem, out Guid guid))
        {
            return guid.ToString("N");
        }

        return NormalizeFileSystemName(stem, "Source note file name");
    }

    private static string CreateGuidV7FileStem()
    {
        return Guid.CreateVersion7().ToString("N");
    }

    private static string CreateShortUniqueIdentifier()
    {
        return Guid.CreateVersion7().ToString("N")[^12..];
    }
}
