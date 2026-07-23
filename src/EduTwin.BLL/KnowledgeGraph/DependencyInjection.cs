using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.KnowledgeGraph;

public static class DependencyInjection
{
    public static IServiceCollection AddKnowledgeGraph(this IServiceCollection services)
    {
        services.AddScoped<IListSubjectsUseCase, ListSubjectsUseCase>();
        services.AddScoped<ICreateSubjectUseCase, CreateSubjectUseCase>();
        services.AddScoped<IGetSubjectUseCase, GetSubjectUseCase>();
        services.AddScoped<IUpdateSubjectUseCase, UpdateSubjectUseCase>();
        services.AddScoped<IDeleteSubjectUseCase, DeleteSubjectUseCase>();
        services.AddScoped<IListKnowledgeNodesUseCase, ListKnowledgeNodesUseCase>();
        services.AddScoped<ICreateKnowledgeNodeUseCase, CreateKnowledgeNodeUseCase>();
        services.AddScoped<IUpdateKnowledgeNodeUseCase, UpdateKnowledgeNodeUseCase>();
        services.AddScoped<IDeleteKnowledgeNodeUseCase, DeleteKnowledgeNodeUseCase>();
        services.AddSingleton<IKnowledgeNodeHierarchyCycleDetector, KnowledgeNodeHierarchyCycleDetector>();
        return services;
    }
}
