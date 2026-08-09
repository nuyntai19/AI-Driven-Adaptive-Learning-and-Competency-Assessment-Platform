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
        services.AddScoped<ICloseAssignmentUseCase, CloseAssignmentUseCase>();
        services.AddScoped<IGetAssignmentProgressUseCase, GetAssignmentProgressUseCase>();
        services.AddScoped<IListStudentAssignmentsUseCase, ListStudentAssignmentsUseCase>();
        services.AddScoped<IGetStudentAssignmentUseCase, GetStudentAssignmentUseCase>();

        return services;
    }
}
