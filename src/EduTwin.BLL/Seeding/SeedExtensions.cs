using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Seeding;

public static class SeedExtensions
{
    public static IServiceCollection AddEduTwinBll(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<EduTwinDbContext>(options =>
            options.UseMySQL(config.GetConnectionString("Default") ?? ""));
        
        services.AddScoped<EduTwinRuntimeSeeder>();
        return services;
    }

    public static async Task ApplyMigrationsAndSeedAsync(this IServiceProvider services, IConfiguration config, bool isDevelopment)
    {
        bool seedEnabled = config.GetValue<bool>("Seed:Enabled");
        if (!seedEnabled) return;

        if (!isDevelopment)
        {
            throw new InvalidOperationException("Seeding is only allowed in Development environment.");
        }

        using var scope = services.CreateScope();
        
        // Migrate
        var dbContext = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
        await dbContext.Database.MigrateAsync();

        // Seed
        var seeder = scope.ServiceProvider.GetRequiredService<EduTwinRuntimeSeeder>();
        await seeder.SeedAsync();
    }
}
