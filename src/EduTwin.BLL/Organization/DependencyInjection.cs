using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.Organization;

public static class DependencyInjection
{
    public static IServiceCollection AddOrganization(this IServiceCollection services)
    {
        services.AddScoped<IGetCenterProfileUseCase, GetCenterProfileUseCase>();
        services.AddScoped<IUpdateCenterProfileUseCase, UpdateCenterProfileUseCase>();
        services.AddScoped<IListTeachersUseCase, ListTeachersUseCase>();
        services.AddScoped<IGetTeacherUseCase, GetTeacherUseCase>();
        services.AddScoped<ICreateTeacherUseCase, CreateTeacherUseCase>();
        services.AddScoped<IUpdateTeacherUseCase, UpdateTeacherUseCase>();
        return services;
    }
}
