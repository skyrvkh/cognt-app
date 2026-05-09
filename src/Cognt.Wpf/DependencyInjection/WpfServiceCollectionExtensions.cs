using Cognt.Wpf.EditorHost;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Documents;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using CogntApp.Features.NoteToCard.ImageOcclusion;
using CogntApp.Shell.Collections;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Decks;
using CogntApp.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CogntApp.DependencyInjection;

internal static class WpfServiceCollectionExtensions
{
    internal static IServiceCollection AddCogntWpfShell(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCogntWpfEditorHost();

        services.AddSingleton<IKnowledgeCollectionPathResolver>(_ => new FileSystemKnowledgeCollectionPathResolver());
        services.AddSingleton<IImageMaskEditorDialogService, ImageMaskEditorDialogService>();
        services.AddSingleton<IDecksWorkspaceDataProvider>(provider =>
            new DecksWorkspaceDataProvider(
                provider.GetRequiredService<IFlashcardDeckSummariesQuery>(),
                provider.GetRequiredService<IFlashcardCardsQuery>(),
                provider.GetRequiredService<INoteService>()));
        services.AddSingleton<AllNotesViewModel>();
        services.AddSingleton<AllNotesView>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<StatisticsSubviewViewModel>();
        services.AddSingleton(provider =>
            new DecksViewModel(
                provider.GetRequiredService<IDecksWorkspaceDataProvider>(),
                provider.GetRequiredService<ICreateFlashcardsCommand>(),
                provider.GetRequiredService<IUpdateFlashcardFrontContentCommand>(),
                provider.GetRequiredService<IKnowledgeDocumentContentFactory>(),
                provider.GetRequiredService<StatisticsSubviewViewModel>()));
        services.AddSingleton<DecksView>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
