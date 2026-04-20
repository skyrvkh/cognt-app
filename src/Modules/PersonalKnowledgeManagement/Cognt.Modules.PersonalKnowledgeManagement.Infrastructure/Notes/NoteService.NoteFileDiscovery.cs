using System.IO;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static bool IsRepositoryMetadataDirectory(string path)
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return string.Equals(name, FilesDirectoryName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, LegacyFilesDirectoryName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, AttachmentsDirectoryName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, FlashcardsDirectoryName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, LegacyFlashcardsDirectoryName, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetKnownLooseNoteDirectoryNames()
    {
        return new[] { FilesDirectoryName, LegacyFilesDirectoryName }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateSupportedNoteFiles(string directoryPath, SearchOption searchOption)
    {
        return Directory
            .EnumerateFiles(directoryPath, $"*{BlockSuiteNoteExtension}", searchOption)
            .Concat(Directory.EnumerateFiles(directoryPath, $"*{MarkdownNoteExtension}", searchOption))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
