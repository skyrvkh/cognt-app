using System.Windows.Controls;

namespace CogntApp.Models.Decks;

public sealed class CreateCardEditorHostRequestedEventArgs(
    ContentControl host,
    string markdown,
    Action<string> contentChanged) : EventArgs
{
    public ContentControl Host { get; } = host;

    public string Markdown { get; } = markdown;

    public Action<string> ContentChanged { get; } = contentChanged;
}
