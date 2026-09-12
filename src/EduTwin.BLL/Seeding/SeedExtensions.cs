using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.IdentityAndTenancy;

namespace EduTwin.BLL.Seeding;

public static class SeedExtensions
{
    public static IServiceCollection AddEduTwinBll(this IServiceCollection services, IConfiguration config)
    {
        var connString = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connString))
        {
            throw new InvalidOperationException("Database connection string is missing or empty.");
        }

        services.AddDbContext<EduTwinDbContext>(options =>
            options.UseMySQL(connString));

        // Register PasswordHasher for dependency injection (R08)
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IManifestEvaluator, ManifestEvaluator>();
        services.AddScoped<EduTwinRuntimeSeeder>();
        services.AddScoped<AuthorizationBootstrapper>();
        services.AddScoped<PlatformAdminProvisioner>();

        return services;
    }

    public static async Task ApplyMigrationsAndSeedAsync(this IServiceProvider services, IConfiguration config, bool isDevelopment)
    {
        using var scope = services.CreateScope();

        // 1. Migrate database schema
        var dbContext = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
        await dbContext.Database.MigrateAsync();

        // 2. Ensure Root Tenant PLATFORM and Platform Administrator are provisioned
        var platformAdminProvisioner = scope.ServiceProvider.GetRequiredService<PlatformAdminProvisioner>();
        await platformAdminProvisioner.EnsureAsync();

        // 3. Demo/Development data seeding (only if enabled in configuration)
        bool seedEnabled = config.GetValue<bool>("Seed:Enabled");
        if (!seedEnabled) return;

        if (!isDevelopment)
        {
            throw new InvalidOperationException("Seeding is only allowed in Development environment.");
        }

        var seeder = scope.ServiceProvider.GetRequiredService<EduTwinRuntimeSeeder>();
        await seeder.SeedAsync();

        var authorizationBootstrapper =
            scope.ServiceProvider.GetRequiredService<AuthorizationBootstrapper>();
        await authorizationBootstrapper.EnsureAsync();
    }
}
