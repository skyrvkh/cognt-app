using System.Windows.Controls;

namespace CogntApp.ViewModels.Decks.PresentationModels;

internal sealed class CreateCardEditorHostRequestedEventArgs(
    ContentControl host,
    string content,
    Action<string> contentChanged) : EventArgs
{
    public ContentControl Host { get; } = host ?? throw new ArgumentNullException(nameof(host));

    public string Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    public Action<string> ContentChanged { get; } = contentChanged ?? throw new ArgumentNullException(nameof(contentChanged));
}
