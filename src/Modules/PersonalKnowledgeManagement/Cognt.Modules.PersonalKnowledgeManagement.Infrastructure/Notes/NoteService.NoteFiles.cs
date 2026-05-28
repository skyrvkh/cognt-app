using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private static void EnsureNotePath(string path)
    {
        if (!IsNoteFilePath(path))
        {
            throw new InvalidOperationException("Mentions can only open note files.");
        }
    }

    private static void EnsureMentionablePath(string path)
    {
        if (!IsMentionableFilePath(path))
        {
            throw new InvalidOperationException("Mentions can only open note or PDF files.");
        }
    }

    private static bool IsNoteFilePath(string path)
    {
        string extension = Path.GetExtension(path);
        return string.Equals(extension, BlockSuiteNoteExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, MarkdownNoteExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMentionableFilePath(string path)
    {
        return IsNoteFilePath(path) || IsPdfPath(path);
    }

    private static bool IsBlockSuiteNotePath(string path)
    {
        return string.Equals(Path.GetExtension(path), BlockSuiteNoteExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPdfPath(string path)
    {
        return string.Equals(Path.GetExtension(path), PdfExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFileSystemName(string name, string label)
    {
        string fileName = name.Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException($"{label} is required.", nameof(name));
        }

        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"{label} contains invalid characters.", nameof(name));
        }

        if (fileName.EndsWith(".", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{label} cannot end with a period.", nameof(name));
        }

        return fileName;
    }

    private static bool TryWriteNewFile(
        string path,
        string content,
        ILogger<NoteService>? logger = null)
    {
        try
        {
            using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write);
            using StreamWriter writer = new(stream, Encoding.UTF8);
            writer.Write(content);
            return true;
        }
        catch (IOException ex) when (File.Exists(path))
        {
            logger?.LogWarning(
                ex,
                "NoteService.TryWriteNewFile skipped because file already exists. Path={Path} ContentLength={ContentLength}",
                path,
                content.Length);
            return false;
        }
    }

    private static void TryDeleteFile(string path, ILogger<NoteService>? logger = null)
    {
        try
        {
            bool existsBeforeDelete = File.Exists(path);

            if (existsBeforeDelete)
            {
                File.Delete(path);
            }

        }
        catch (IOException ex)
        {
            logger?.LogWarning(ex, "NoteService.TryDeleteFile failed with IO error. Path={Path}", path);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.LogWarning(ex, "NoteService.TryDeleteFile failed with unauthorized access. Path={Path}", path);
        }
    }

    private static string EnsureTrailingNewline(string content)
    {
        return content.Length == 0 || content.EndsWith(Environment.NewLine, StringComparison.Ordinal)
            ? content
            : $"{content}{Environment.NewLine}";
    }
}
