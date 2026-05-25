using Cognt.Wpf.EditorHost;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using CogntApp.Design;
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

        services.AddSingleton<IThemeService, WpfThemeService>();
        services.AddSingleton<IEditorSettingsService, WpfEditorSettingsService>();
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
        services.AddSingleton<GraphViewModel>();
        services.AddSingleton<GraphView>();
        services.AddSingleton<NoteInspectorViewModel>();
        services.AddSingleton<NoteInspectorView>();
        services.AddSingleton<NoteGraphToolView>();
        services.AddSingleton<StatisticsSubviewViewModel>();
        services.AddSingleton(provider =>
            new DecksViewModel(
                provider.GetRequiredService<IDecksWorkspaceDataProvider>(),
                provider.GetRequiredService<IUpdateFlashcardSchedulingCommand>(),
                provider.GetRequiredService<IUpdateFlashcardContentCommand>(),
                provider.GetRequiredService<IDeleteFlashcardsCommand>(),
                provider.GetRequiredService<IUpdateFlashcardTagsCommand>(),
                provider.GetRequiredService<INoteService>(),
                provider.GetRequiredService<IEditorViewFactory>(),
                provider.GetRequiredService<StatisticsSubviewViewModel>()));
        services.AddSingleton<DecksView>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
