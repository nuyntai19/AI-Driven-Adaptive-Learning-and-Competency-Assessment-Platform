using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.DigitalTwin;

public static class DependencyInjection
{
    public static IServiceCollection AddDigitalTwin(this IServiceCollection services)
    {
        services.AddScoped<IGoalIdGenerator, CryptographicGoalIdGenerator>();
        services.AddScoped<IUpsertStudentSubjectGoalUseCase, UpsertStudentSubjectGoalUseCase>();
        return services;
    }
}
