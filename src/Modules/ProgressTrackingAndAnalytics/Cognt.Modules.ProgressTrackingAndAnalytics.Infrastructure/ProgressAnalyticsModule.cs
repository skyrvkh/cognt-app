using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

public static class ProgressAnalyticsModule
{
    public static IServiceCollection AddProgressTrackingAndAnalyticsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ICollectionProgressAnalyticsDataSource, SpacedRepetitionSystemCollectionProgressAnalyticsDataSource>();
        services.AddSingleton<ICollectionProgressAnalyticsReportQuery, GetCollectionProgressAnalyticsReport>();
        services.AddSingleton<ICollectionProgressAnalyticsDashboardQuery, GetCollectionProgressAnalyticsDashboard>();

        return services;
    }
}
