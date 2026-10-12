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
        services.AddScoped<IPlatformAuditService, PlatformAuditService>();
        services.AddScoped<IPlatformMeService, PlatformMeService>();
        services.AddScoped<Operations.IPlatformOperationsService, Operations.PlatformOperationsService>();
        services.AddScoped<Operations.OperationsMaintenance>();
        services.AddSingleton<Operations.OperationalTelemetry>();
        services.AddSingleton<Operations.IAiUsageRecorder>(sp => sp.GetRequiredService<Operations.OperationalTelemetry>());
        services.AddSingleton<Operations.OperationsQueryCache>();
        services.AddOptions<Operations.PlatformOperationsOptions>().Bind(configuration.GetSection("PlatformOperations"))
            .Configure(options =>
            {
                var prices = configuration["PLATFORM_AI_PRICES_JSON"];
                if (!string.IsNullOrWhiteSpace(prices))
                {
                    try { options.Prices = System.Text.Json.JsonSerializer.Deserialize<List<Operations.AiPriceProfile>>(prices,
                        new System.Text.Json.JsonSerializerOptions {PropertyNameCaseInsensitive = true}) ?? []; }
                    catch (System.Text.Json.JsonException) { options.Prices = []; } // Unknown cost; never block learning on telemetry configuration.
                }
            });

        return services;
    }
}
