namespace CogntApp.Models.Markdown;

public sealed record CreateExtractFileRequest(
    string RootDirectory,
    string CurrentNotePath,
    string SourcePath,
    string SelectedContent);

public sealed record MentionTarget(
    string RelativePath,
    string FileName,
    int? RecentRank);

public sealed record MentionPreview(
    string RelativePath,
    string Title,
    string MarkdownExcerpt);

public sealed record NoteGroupResult(
    string FolderPath,
    string TargetNotePath,
    string DroppedNotePath);
