using System.Windows;
using System.Windows.Input;

namespace Cognt.Wpf.EditorHost.Abstractions;

public interface ITransientEditorHost
{
    void Mount(UIElement element);

    void PrepareForInput();

    void UpdateHostLayout();

    void FocusHost();

    void SetFocusedElement(IInputElement element);
}
