using Cognt.Modules.SpacedRepetitionSystem.Application.Abstractions;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;
using Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure;

public static class SpacedRepetitionModule
{
    public static IServiceCollection AddSpacedRepetitionSystemModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<FsrsFlashcardScheduler>();
        services.AddSingleton<IFlashcardScheduler>(provider =>
            provider.GetRequiredService<FsrsFlashcardScheduler>());
        services.AddSingleton<ICramSessionEngine>(_ => new CramSessionEngine());
        services.AddSingleton<SqliteFlashcardStore>(provider =>
            new SqliteFlashcardStore(
                provider.GetRequiredService<IFlashcardScheduler>(),
                provider.GetService<ILogger<SqliteFlashcardStore>>()));
        services.AddSingleton<IFlashcardDeckReader>(provider => provider.GetRequiredService<SqliteFlashcardStore>());
        services.AddSingleton<IFlashcardDashboardReader>(provider => provider.GetRequiredService<SqliteFlashcardStore>());
        services.AddSingleton<IFlashcardReviewReader>(provider => provider.GetRequiredService<SqliteFlashcardStore>());
        services.AddSingleton<IFlashcardNoteGenerationReader>(provider => provider.GetRequiredService<SqliteFlashcardStore>());
        services.AddSingleton<IFlashcardAuthoringWriter>(provider => provider.GetRequiredService<SqliteFlashcardStore>());
        services.AddSingleton<IFlashcardReviewWriter>(provider => provider.GetRequiredService<SqliteFlashcardStore>());
        services.AddSingleton<IFlashcardCollectionImportWriter>(provider => provider.GetRequiredService<SqliteFlashcardStore>());

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
        services.AddSingleton<IImportFlashcardCollectionCommand>(provider =>
            new ImportFlashcardCollection(provider.GetRequiredService<IFlashcardCollectionImportWriter>()));
        services.AddSingleton<IUpdateFlashcardReviewSettingsCommand>(provider =>
            new UpdateFlashcardReviewSettings(provider.GetRequiredService<IFlashcardReviewWriter>()));
        services.AddSingleton<IUpdateFlashcardFrontContentCommand>(provider =>
            new UpdateFlashcardFrontContent(provider.GetRequiredService<IFlashcardAuthoringWriter>()));
        services.AddSingleton<IUpdateFlashcardContentCommand>(provider =>
            new UpdateFlashcardContent(provider.GetRequiredService<IFlashcardAuthoringWriter>()));
        services.AddSingleton<IDeleteFlashcardsCommand>(provider =>
            new DeleteFlashcards(provider.GetRequiredService<IFlashcardAuthoringWriter>()));
        services.AddSingleton<IDeleteFlashcardDeckCommand>(provider =>
            new DeleteFlashcardDeck(provider.GetRequiredService<IFlashcardAuthoringWriter>()));
        services.AddSingleton<IUpdateFlashcardTagsCommand>(provider =>
            new UpdateFlashcardTags(provider.GetRequiredService<IFlashcardAuthoringWriter>()));
        services.AddSingleton<IUpdateFlashcardSchedulingCommand>(provider =>
            new UpdateFlashcardScheduling(provider.GetRequiredService<IFlashcardReviewWriter>()));
        services.AddSingleton<IRecordFlashcardReviewCommand>(provider =>
            new RecordFlashcardReview(provider.GetRequiredService<IFlashcardReviewWriter>()));

        return services;
    }
}
