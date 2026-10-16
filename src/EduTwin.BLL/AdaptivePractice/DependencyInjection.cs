using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.AdaptivePractice;

public static class DependencyInjection
{
    public static IServiceCollection AddAdaptivePractice(this IServiceCollection services)
    {
        services.AddScoped<IAdaptivePracticeEngine, AdaptivePracticeEngine>();
        return services;
    }
}
