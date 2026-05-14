using Cognt.Wpf.EditorHost.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Cognt.Wpf.EditorHost;

internal sealed class EditorViewFactory(IServiceProvider serviceProvider) : IEditorViewFactory
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public EditorView Create()
    {
        return ActivatorUtilities.CreateInstance<EditorView>(serviceProvider);
    }
}
