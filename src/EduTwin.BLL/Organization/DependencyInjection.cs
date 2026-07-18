using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.Organization;

public static class DependencyInjection
{
    public static IServiceCollection AddOrganization(this IServiceCollection services)
    {
        services.AddScoped<IGetCenterProfileUseCase, GetCenterProfileUseCase>();
        return services;
    }
}
