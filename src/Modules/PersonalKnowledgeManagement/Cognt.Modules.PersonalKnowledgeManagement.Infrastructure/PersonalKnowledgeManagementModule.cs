using Cognt.Modules.PersonalKnowledgeManagement.Application.Documents;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Search;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Documents;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;
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
}
