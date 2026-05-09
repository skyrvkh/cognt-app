using Cognt.Wpf.EditorHost.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Cognt.Wpf.EditorHost;

public static class EditorHostServiceCollectionExtensions
{
    public static IServiceCollection AddCogntWpfEditorHost(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWpfBlazorWebView();
        services.AddSingleton<EditorView>();

        return services;
    }
}
