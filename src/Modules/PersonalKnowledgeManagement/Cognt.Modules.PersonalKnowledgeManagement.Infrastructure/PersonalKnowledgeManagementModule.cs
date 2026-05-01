using Cognt.Modules.PersonalKnowledgeManagement.Application.Documents;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Search;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Search;
using Microsoft.Extensions.DependencyInjection;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure;

public static class PersonalKnowledgeManagementModule
{
    public static IServiceCollection AddPersonalKnowledgeManagementModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDocumentServices();
        services.AddNoteServices();
        services.AddSearchServices();

        return services;
    }

    private static IServiceCollection AddDocumentServices(this IServiceCollection services)
    {
        services.AddSingleton<IKnowledgeDocumentContentFactory>(_ => new KnowledgeDocumentContentFactory());

        return services;
    }

    private static IServiceCollection AddNoteServices(this IServiceCollection services)
    {
        services.AddSingleton(_ => new NoteService());

        return services;
    }

    private static IServiceCollection AddSearchServices(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
            new GlobalSearchService(provider.GetRequiredService<NoteService>()));
        services.AddSingleton<IGlobalSearchService>(provider =>
            provider.GetRequiredService<GlobalSearchService>());

        return services;
    }
}
