using System.Windows.Controls;

namespace CogntApp.Models.Decks;

public sealed class CreateCardEditorHostRequestedEventArgs(
    ContentControl host,
    string content,
    Action<string> contentChanged) : EventArgs
{
    public ContentControl Host { get; } = host;

    public string Content { get; } = content;

    public Action<string> ContentChanged { get; } = contentChanged;
}
