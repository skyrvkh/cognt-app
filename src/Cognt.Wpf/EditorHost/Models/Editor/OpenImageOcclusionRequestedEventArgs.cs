namespace Cognt.Wpf.EditorHost.Models.Editor;

public sealed class OpenImageOcclusionRequestedEventArgs : EventArgs
{
    public OpenImageOcclusionRequestedEventArgs(
        string sourcePath,
        string imageBlockId,
        string imageRelPath,
        string content)
    {
        SourcePath = sourcePath;
        ImageBlockId = imageBlockId;
        ImageRelPath = imageRelPath;
        Content = content;
    }

    public string SourcePath { get; }

    public string ImageBlockId { get; }

    public string ImageRelPath { get; }

    public string Content { get; }
}
