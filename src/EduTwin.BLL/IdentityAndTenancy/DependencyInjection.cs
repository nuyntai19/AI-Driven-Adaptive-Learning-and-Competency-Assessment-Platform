using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.IdentityAndTenancy;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityAndTenancy(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextInitializer>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<IBackgroundTenantScopeFactory>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>(sp => sp.GetRequiredService<TenantContext>());

        services.AddScoped<IClaimsResolver, ClaimsResolver>();
        services.AddScoped<ICenterResolver, CenterResolver>();

        return services;
    }
}
