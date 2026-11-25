using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.BLL.Notifications;

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

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<NotificationBusinessInterceptor>();
        services.AddDbContext<EduTwinDbContext>((provider, options) =>
            options.UseMySQL(connString).AddInterceptors(provider.GetRequiredService<NotificationBusinessInterceptor>()));

        // Register PasswordHasher for dependency injection (R08)
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IManifestEvaluator, ManifestEvaluator>();
        services.AddScoped<EduTwinRuntimeSeeder>();
        services.AddScoped<SeedQuestionGradeBackfill>();
        services.AddScoped<SeedSubjectCodeBackfill>();
        services.AddScoped<ExpandedDemoSeedBackfill>();
        services.AddScoped<AuthorizationBootstrapper>();
        services.AddScoped<DefaultSystemRolePermissionBackfill>();
        services.AddScoped<PlatformAdminProvisioner>();

        return services;
    }

    public static async Task ApplyMigrationsAndSeedAsync(
        this IServiceProvider services,
        IConfiguration config,
        bool isDevelopment,
        bool isStagingSeedAllowed = false,
        bool? seedEnabledOverride = null)
    {
        using var scope = services.CreateScope();

        // 1. Migrate database schema
        // Managed MySQL 8.4 providers (e.g. Aiven) enable sql_require_primary_key=ON by default,
        // which blocks historical migrations creating temporary preflight tables without a PK.
        // Scope the session override strictly to MigrateAsync without altering any historical migration.
        var dbContext = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
        var isMySql = dbContext.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;
        var connection = isMySql ? dbContext.Database.GetDbConnection() : null;
        var openedForMigration = false;
        if (connection is not null)
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
                openedForMigration = true;
            }
            await using var disableReqPk = connection.CreateCommand();
            disableReqPk.CommandText = "SET SESSION sql_require_primary_key = 0;";
            try { await disableReqPk.ExecuteNonQueryAsync(); } catch { /* Provider may not support variable */ }
        }
        try
        {
            await dbContext.Database.MigrateAsync();
        }
        finally
        {
            if (connection is not null && connection.State == System.Data.ConnectionState.Open)
            {
                try
                {
                    await using var restoreReqPk = connection.CreateCommand();
                    restoreReqPk.CommandText = "SET SESSION sql_require_primary_key = DEFAULT;";
                    await restoreReqPk.ExecuteNonQueryAsync();
                }
                catch { /* Best-effort restore */ }
                if (openedForMigration)
                {
                    await connection.CloseAsync();
                }
            }
        }

        // 2. Ensure Root Tenant PLATFORM and Platform Administrator are provisioned
        var platformAdminProvisioner = scope.ServiceProvider.GetRequiredService<PlatformAdminProvisioner>();
        await platformAdminProvisioner.EnsureAsync();

        // Existing system roles need default grants after a code upgrade even in
        // Production / Seed:Enabled=false. This path is additive and preserves
        // custom roles, revoked assignments and inactive roles/permissions.
        await scope.ServiceProvider.GetRequiredService<DefaultSystemRolePermissionBackfill>().EnsureAsync();

        // 3. Demo/Development data seeding (only if enabled in configuration or explicit init command)
        bool seedEnabled = seedEnabledOverride ?? config.GetValue<bool>("Seed:Enabled");
        if (!seedEnabled) return;

        if (!isDevelopment && !isStagingSeedAllowed)
        {
            throw new InvalidOperationException("Seeding is only allowed in Development environment or with explicit StagingInit:AllowSeed.");
        }

        await scope.ServiceProvider.GetRequiredService<SeedQuestionGradeBackfill>().EnsureAsync();
        // Must run before the manifest check: earlier seeds stored the English subject as ENGLISH.
        await scope.ServiceProvider.GetRequiredService<SeedSubjectCodeBackfill>().EnsureAsync();
        var seeder = scope.ServiceProvider.GetRequiredService<EduTwinRuntimeSeeder>();
        await seeder.SeedAsync();
        await scope.ServiceProvider.GetRequiredService<ExpandedDemoSeedBackfill>().EnsureAsync();

        var authorizationBootstrapper =
            scope.ServiceProvider.GetRequiredService<AuthorizationBootstrapper>();
        await authorizationBootstrapper.EnsureAsync();
    }
}
