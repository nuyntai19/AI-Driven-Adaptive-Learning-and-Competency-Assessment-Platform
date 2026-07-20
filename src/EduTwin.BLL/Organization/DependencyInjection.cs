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
        services.AddScoped<IDeleteTeacherUseCase, DeleteTeacherUseCase>();
        services.AddScoped<IListStudentsUseCase, ListStudentsUseCase>();
        services.AddScoped<IGetStudentUseCase, GetStudentUseCase>();
        services.AddScoped<ICreateStudentUseCase, CreateStudentUseCase>();
        services.AddScoped<IUpdateStudentUseCase, UpdateStudentUseCase>();
        services.AddScoped<IListClassesUseCase, ListClassesUseCase>();
        services.AddScoped<IGetClassUseCase, GetClassUseCase>();
        services.AddScoped<ICreateClassUseCase, CreateClassUseCase>();
        services.AddScoped<IUpdateClassUseCase, UpdateClassUseCase>();
        services.AddScoped<IAddStudentsToClassUseCase, AddStudentsToClassUseCase>();
        services.AddScoped<IRemoveStudentFromClassUseCase, RemoveStudentFromClassUseCase>();
        return services;
    }
}
