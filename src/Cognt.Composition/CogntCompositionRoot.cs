using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure;
using Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Cognt.Composition;

public static class CogntServiceCollectionExtensions
{
    public static IServiceCollection AddCogntModules(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddPersonalKnowledgeManagementModule()
            .AddProgressTrackingAndAnalyticsModule()
            .AddSpacedRepetitionSystemModule();
    }
}
