using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EduTwin.BLL.Platform;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PlatformBootstrapOptions>()
            .Bind(configuration.GetSection(PlatformBootstrapOptions.SectionName));
        services.AddSingleton<IValidateOptions<PlatformBootstrapOptions>, PlatformBootstrapOptionsValidator>();

        services.AddScoped<IPlatformCenterService, PlatformCenterService>();

        return services;
    }
}
