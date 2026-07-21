using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.KnowledgeGraph;

public static class DependencyInjection
{
    public static IServiceCollection AddKnowledgeGraph(this IServiceCollection services)
    {
        services.AddScoped<IListSubjectsUseCase, ListSubjectsUseCase>();
        return services;
    }
}
