using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.Assignments;

public static class DependencyInjection
{
    public static IServiceCollection AddAssignments(this IServiceCollection services)
    {
        services.AddScoped<ICreateAssignmentUseCase, CreateAssignmentUseCase>();
        services.AddScoped<IGetAssignmentUseCase, GetAssignmentUseCase>();
        services.AddScoped<IListAssignmentsUseCase, ListAssignmentsUseCase>();
        services.AddScoped<IUpdateAssignmentUseCase, UpdateAssignmentUseCase>();
        services.AddScoped<IPublishAssignmentUseCase, PublishAssignmentUseCase>();

        return services;
    }
}
