using CogntApp.Models.Markdown;

namespace CogntApp.Models.Editor;

public sealed class CreateLinkedNoteRequestedEventArgs(
    string sourcePath,
    string viewMode) : EventArgs
{
    public string SourcePath { get; } = sourcePath;

    public string ViewMode { get; } = viewMode;

    public CreateLinkedNoteResult Result { get; private set; } =
        CreateLinkedNoteResult.Fail("No linked note handler is registered.");

    public void Complete(CreateLinkedNoteResult result)
    {
        Result = result;
    }
}

public sealed record CreateLinkedNoteResult(
    bool Ok,
    string? RelativePath = null,
    string? Title = null,
    string? DocumentContent = null,
    string? Error = null)
{
    public static CreateLinkedNoteResult Success(LinkedNote note)
    {
        return new CreateLinkedNoteResult(true, note.RelativePath, note.Title, note.DocumentContent);
    }

    public static CreateLinkedNoteResult Fail(string error)
    {
        return new CreateLinkedNoteResult(false, Error: error);
    }
}
