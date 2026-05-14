using Cognt.Wpf.EditorHost.Views;

namespace Cognt.Wpf.EditorHost;

public interface IEditorViewFactory
{
    EditorView Create();
}
