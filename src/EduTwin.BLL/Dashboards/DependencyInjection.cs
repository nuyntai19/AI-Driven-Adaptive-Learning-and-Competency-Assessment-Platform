using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EduTwin.BLL.Dashboards;

public static class DependencyInjection
{
    public static IServiceCollection AddDashboards(this IServiceCollection services)
    {
        services.TryAddScoped<IGetCenterDashboardUseCase, GetCenterDashboardUseCase>();
        services.TryAddScoped<IGetStudentDashboardUseCase, GetStudentDashboardUseCase>();
        services.TryAddScoped<StudentAcademicScopeReader>();
        services.TryAddScoped<IGetStudentWorkspaceSummaryUseCase, GetStudentWorkspaceSummaryUseCase>();
        services.TryAddScoped<IGetClassDashboardUseCase, GetClassDashboardUseCase>();
        return services;
    }
}
