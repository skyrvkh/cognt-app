using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;

public static class ProgressAnalyticsModule
{
    public static IServiceCollection AddProgressTrackingAndAnalyticsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISpacedRepetitionSystemProgressAnalyticsSnapshotMapper, SpacedRepetitionSystemProgressAnalyticsSnapshotMapper>();
        services.AddSingleton<ICollectionProgressAnalyticsDataSource>(provider =>
        {
            return
            new SpacedRepetitionSystemCollectionProgressAnalyticsDataSource(
                provider.GetRequiredService<IFlashcardDeckSummariesQuery>(),
                provider.GetRequiredService<IFlashcardDashboardSummaryQuery>(),
                provider.GetRequiredService<IFlashcardProgressAnalyticsQuery>(),
                provider.GetRequiredService<ISpacedRepetitionSystemProgressAnalyticsSnapshotMapper>());
        });
        services.AddSingleton<ICollectionProgressAnalyticsReportQuery>(provider =>
            new GetCollectionProgressAnalyticsReport(provider.GetRequiredService<ICollectionProgressAnalyticsDataSource>()));
        services.AddSingleton<ICollectionProgressAnalyticsDashboardQuery>(provider =>
            new GetCollectionProgressAnalyticsDashboard(provider.GetRequiredService<ICollectionProgressAnalyticsDataSource>()));

        return services;
    }
}
