namespace CogntApp.Models.Markdown;

public sealed record CreateExtractFileRequest(
    string RootDirectory,
    string CurrentNotePath,
    string SourcePath,
    string SelectedMarkdown);

public sealed record MarkdownMentionTarget(
    string RelativePath,
    string FileName,
    int? RecentRank);

public sealed record MarkdownMentionPreview(
    string RelativePath,
    string Title,
    string MarkdownExcerpt);

public sealed record MarkdownNoteGroupResult(
    string FolderPath,
    string TargetNotePath,
    string DroppedNotePath);
