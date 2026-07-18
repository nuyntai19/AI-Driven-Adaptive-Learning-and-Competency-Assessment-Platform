using Microsoft.Extensions.DependencyInjection;
using EduTwin.DAL.IdentityAndTenancy;

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

        services.AddScoped<ILoginUseCase, LoginUseCase>();
        services.AddScoped<IRefreshTokenCodec, RefreshTokenCodec>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IRefreshUseCase, RefreshUseCase>();
        services.AddScoped<ILogoutUseCase, LogoutUseCase>();
        services.AddScoped<IGetCurrentUserUseCase, GetCurrentUserUseCase>();

        services.AddScoped<OrganizationOwnershipGuard>();
        services.AddScoped<ITeacherOwnershipGuard>(sp => sp.GetRequiredService<OrganizationOwnershipGuard>());
        services.AddScoped<IClassOwnershipGuard>(sp => sp.GetRequiredService<OrganizationOwnershipGuard>());
        services.AddScoped<IStudentOwnershipGuard>(sp => sp.GetRequiredService<OrganizationOwnershipGuard>());
        return services;
    }
}
