using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure;

public static class SpacedRepetitionModule
{
    public static IServiceCollection AddSpacedRepetitionSystemModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IFlashcardScheduler>(_ => new FsrsFlashcardScheduler());
        services.AddSingleton<ICramSessionEngine>(_ => new CramSessionEngine());
        services.AddSingleton<SqliteFlashcardStore>(provider =>
            new SqliteFlashcardStore(provider.GetRequiredService<IFlashcardScheduler>()));
        services.AddSingleton<IFlashcardDeckReader>(provider => provider.GetRequiredService<SqliteFlashcardStore>());
        services.AddSingleton<IFlashcardDashboardReader>(provider => provider.GetRequiredService<SqliteFlashcardStore>());
        services.AddSingleton<IFlashcardReviewReader>(provider => provider.GetRequiredService<SqliteFlashcardStore>());
        services.AddSingleton<IFlashcardNoteGenerationReader>(provider => provider.GetRequiredService<SqliteFlashcardStore>());
        services.AddSingleton<IFlashcardAuthoringWriter>(provider => provider.GetRequiredService<SqliteFlashcardStore>());
        services.AddSingleton<IFlashcardReviewWriter>(provider => provider.GetRequiredService<SqliteFlashcardStore>());

        services.AddSingleton<IFlashcardDeckSummariesQuery>(provider =>
            new GetFlashcardDeckSummaries(provider.GetRequiredService<IFlashcardDeckReader>()));
        services.AddSingleton<IFlashcardCardsQuery>(provider =>
            new GetFlashcardCards(provider.GetRequiredService<IFlashcardDeckReader>()));
        services.AddSingleton<IFlashcardDashboardSummaryQuery>(provider =>
            new GetFlashcardDashboardSummary(provider.GetRequiredService<IFlashcardDashboardReader>()));
        services.AddSingleton<IFlashcardProgressAnalyticsQuery>(provider =>
            new GetFlashcardProgressAnalytics(provider.GetRequiredService<IFlashcardDashboardReader>()));
        services.AddSingleton<INoteToCardQueueQuery>(provider =>
            new GetNoteToCardQueue(provider.GetRequiredService<IFlashcardNoteGenerationReader>()));
        services.AddSingleton<IFlashcardReviewSettingsQuery>(provider =>
            new GetFlashcardReviewSettings(provider.GetRequiredService<IFlashcardReviewReader>()));
        services.AddSingleton<IFlashcardReviewQueueQuery>(provider =>
            new GetFlashcardReviewQueue(provider.GetRequiredService<IFlashcardReviewReader>()));

        services.AddSingleton<ICreateFlashcardsCommand>(provider =>
            new CreateFlashcards(provider.GetRequiredService<IFlashcardAuthoringWriter>()));
        services.AddSingleton<ICreateCardsFromCardContextCommand>(provider =>
            new CreateCardsFromCardContext(provider.GetRequiredService<IFlashcardAuthoringWriter>()));
        services.AddSingleton<IUpdateFlashcardReviewSettingsCommand>(provider =>
            new UpdateFlashcardReviewSettings(provider.GetRequiredService<IFlashcardReviewWriter>()));
        services.AddSingleton<IUpdateFlashcardFrontContentCommand>(provider =>
            new UpdateFlashcardFrontContent(provider.GetRequiredService<IFlashcardAuthoringWriter>()));
        services.AddSingleton<IRecordFlashcardReviewCommand>(provider =>
            new RecordFlashcardReview(provider.GetRequiredService<IFlashcardReviewWriter>()));

        return services;
    }
}
