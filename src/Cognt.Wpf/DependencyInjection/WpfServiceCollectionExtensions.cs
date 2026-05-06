using CogntApp.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CogntApp.DependencyInjection;

internal static class WpfServiceCollectionExtensions
{
    internal static IServiceCollection AddCogntWpfShell(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<DecksView>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
