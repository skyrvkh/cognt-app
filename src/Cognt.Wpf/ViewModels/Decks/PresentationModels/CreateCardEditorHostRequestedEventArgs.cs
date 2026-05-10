using Cognt.Wpf.EditorHost.Abstractions;

namespace CogntApp.ViewModels.Decks.PresentationModels;

internal sealed class CreateCardEditorHostRequestedEventArgs(
    ITransientEditorHost host,
    string content,
    Action<string> contentChanged) : EventArgs
{
    public ITransientEditorHost Host { get; } = host ?? throw new ArgumentNullException(nameof(host));

    public string Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    public Action<string> ContentChanged { get; } = contentChanged ?? throw new ArgumentNullException(nameof(contentChanged));
}
