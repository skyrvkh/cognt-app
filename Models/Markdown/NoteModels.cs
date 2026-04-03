namespace CogntApp.Models.Markdown;

public sealed record MentionTarget(
    string RelativePath,
    string Title,
    string FileName,
    int? RecentRank)
{
    public MentionTarget(string relativePath, string fileName, int? recentRank)
        : this(relativePath, fileName, fileName, recentRank)
    {
    }
}

public sealed record MentionPreview(
    string RelativePath,
    string Title,
    string DocumentContent)
{
    public string MarkdownExcerpt => DocumentContent;
}

public sealed record LinkedNote(
    string NotePath,
    string RelativePath,
    string Title,
    string DocumentContent);

public sealed record CreatedNote(
    long ItemId,
    string NotePath,
    string FileRelPath);


public sealed record CreateExtractFileRequest(
    string NotesDirectory,
    string CurrentNotePath,
    string SourcePath,
    string SelectedContent)
{
    public string RootDirectory => NotesDirectory;
}

public sealed record NoteGroupResult(
    string GroupFolderPath,
    string TargetNotePath,
    string DroppedNotePath);
