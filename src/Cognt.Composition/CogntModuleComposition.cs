using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Domain;
using Cognt.Modules.ProgressTrackingAndAnalytics.Application.Abstractions;
using Cognt.Modules.ProgressTrackingAndAnalytics.Infrastructure;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using Cognt.Modules.SpacedRepetitionSystem.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Cognt.Composition;

public sealed record CogntModuleSlice(string Name, Type AnchorType);

public static class CogntModuleComposition
{
    public static IServiceCollection AddCogntModules(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IReadOnlyList<CogntModuleSlice>>(
            new[]
            {
                new CogntModuleSlice("PersonalKnowledgeManagement.Domain", typeof(KnowledgeTreeItemRow)),
                new CogntModuleSlice("PersonalKnowledgeManagement.Application", typeof(KnowledgeOpenedNoteReadModel)),
                new CogntModuleSlice("ProgressTrackingAndAnalytics.Application", typeof(CollectionProgressAnalyticsSnapshot)),
                new CogntModuleSlice("ProgressTrackingAndAnalytics.Infrastructure", typeof(ProgressAnalyticsModule)),
                new CogntModuleSlice("SpacedRepetitionSystem.Domain", typeof(FlashcardSchedule)),
            });

        return services;
    }

    public static IDisposable BuildModuleScope()
    {
        ServiceCollection services = [];
        services.AddCogntModules();
        RunCardContextSnapshotCheck();
        return services.BuildServiceProvider();
    }

    private static void RunCardContextSnapshotCheck()
    {
        CardContextSnapshot snapshot = CardContextSnapshotFactory.CreateSnapshot(
            "startup-check.json",
            string.Empty,
            new CardContext(
                "startup-check",
                null,
                [],
                [],
                [],
                [],
                null,
                CardContextStatus.Draft,
                null));

        if (snapshot.Status != CardContextStatus.Draft)
        {
            throw new InvalidOperationException("Card context snapshot startup check failed.");
        }
    }
}
