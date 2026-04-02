using Microsoft.Extensions.DependencyInjection;

namespace CogntApp.Services;

internal static class BlazorHybridServices
{
    private static readonly Lazy<IServiceProvider> SharedServices = new(CreateServices);

    public static IServiceProvider Services => SharedServices.Value;

    private static IServiceProvider CreateServices()
    {
        ServiceCollection services = new();
        services.AddWpfBlazorWebView();
        return services.BuildServiceProvider();
    }
}
