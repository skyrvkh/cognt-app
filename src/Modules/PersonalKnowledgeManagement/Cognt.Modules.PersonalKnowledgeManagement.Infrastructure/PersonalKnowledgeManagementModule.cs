using Cognt.Modules.PersonalKnowledgeManagement.Application.Documents;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Safety;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Search;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Safety;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure;

public static class PersonalKnowledgeManagementModule
{
    public static IServiceCollection AddPersonalKnowledgeManagementModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDocumentServices();
        services.AddNoteServices();
        services.AddSearchServices();
        services.AddSafetyServices();

        return services;
    }

    private static IServiceCollection AddDocumentServices(this IServiceCollection services)
    {
        services.AddSingleton<IKnowledgeDocumentContentFactory>(provider =>
            new KnowledgeDocumentContentFactory(provider.GetService<ILogger<KnowledgeDocumentContentFactory>>()));

        return services;
    }

    private static IServiceCollection AddNoteServices(this IServiceCollection services)
    {
        services.AddSingleton<INoteService>(provider =>
            new NoteService(provider.GetService<ILogger<NoteService>>()));

        return services;
    }

    private static IServiceCollection AddSearchServices(this IServiceCollection services)
    {
        services.AddSingleton<IGlobalSearchService>(provider =>
            new GlobalSearchService(
                provider.GetRequiredService<INoteService>(),
                provider.GetService<ILogger<GlobalSearchService>>()));

        return services;
    }

    /// <summary>
    /// Registers the collection-safety surface: backup, restore,
    /// trash, and permanent-deletion. The application layer sees
    /// only <see cref="ICollectionSafetyService"/>; the concrete
    /// services stay in the infrastructure layer.
    /// </summary>
    private static IServiceCollection AddSafetyServices(this IServiceCollection services)
    {
        services.AddSingleton<IRecycleBinAdapter, StagingRecycleBinAdapter>();
        services.AddSingleton<TrashCollectionState>();
        services.AddSingleton<BackupArchiveService>();
        services.AddSingleton<RestoreArchiveService>();
        services.AddSingleton<TrashArchiveService>();
        services.AddSingleton<ICollectionSafetyService>(provider => new CollectionSafetyService(
            backupExecutor: (r, c) => provider.GetRequiredService<BackupArchiveService>().CreateAsync(r, c),
            restoreExecutor: (r, c) => provider.GetRequiredService<RestoreArchiveService>().RestoreAsync(r, c),
            trashExecutor: (r, c) => provider.GetRequiredService<TrashArchiveService>().SoftDeleteAsync(r, c),
            trashRestoreExecutor: (r, c) => provider.GetRequiredService<TrashArchiveService>().RestoreFromTrashAsync(r, c),
            emptyTrashExecutor: (r, c) => provider.GetRequiredService<TrashArchiveService>().EmptyTrashAsync(r, c),
            permanentDeleteExecutor: (r, c) => provider.GetRequiredService<TrashArchiveService>().PermanentDeleteAsync(r, c)));
        return services;
    }
}
