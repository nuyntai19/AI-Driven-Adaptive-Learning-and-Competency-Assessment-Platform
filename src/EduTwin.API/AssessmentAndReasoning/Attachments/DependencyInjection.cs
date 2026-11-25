using EduTwin.API.Security;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.Seeding;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.Attachments;

public static class DependencyInjection
{
    public static IServiceCollection AddAttemptAttachmentStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AttachmentStorageOptions>().Bind(configuration.GetSection(AttachmentStorageOptions.SectionName));
        var options = configuration.GetSection(AttachmentStorageOptions.SectionName)
            .Get<AttachmentStorageOptions>() ?? new AttachmentStorageOptions();

        var envName = RuntimeEnvironmentSecurity.ResolveEnvironmentName(configuration);
        var isDevelopment = string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase);
        var applicationName = isDevelopment ? "EduTwin" : $"EduTwin-{envName}";

        var dataProtection = services.AddDataProtection().SetApplicationName(applicationName);
        if (!string.IsNullOrWhiteSpace(options.DataProtectionKeysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(options.DataProtectionKeysPath));
        }
        else if (ShouldUseDatabaseKeyRing(options, configuration, isDevelopment))
        {
            var connString = configuration.GetConnectionString("Default")!;
            var encryptionSecret = options.DataProtectionKeyEncryptionSecret
                ?? configuration["DataProtection:EncryptionSecret"];

            services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<MySqlDataProtectionKeyRepository>>();
                var repo = new MySqlDataProtectionKeyRepository(connString, encryptionSecret, logger);
                return new ConfigureOptions<KeyManagementOptions>(keyOptions =>
                {
                    keyOptions.XmlRepository = repo;
                });
            });
        }

        services.TryAddSingleton<IAttemptAttachmentTokenService, DataProtectionAttemptAttachmentTokenService>();
        services.TryAddSingleton<FileSystemAttemptAttachmentStorage>();

        if (string.Equals(options.Provider, "Cloud", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(options.Provider, "Supabase", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient(SupabaseStorageClient.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(2)
                });
            services.TryAddSingleton<ICloudObjectStorageClient, SupabaseStorageClient>();
            services.TryAddSingleton<IAttemptAttachmentStorage, CloudAttemptAttachmentStorage>();
        }
        else
        {
            services.TryAddSingleton<IAttemptAttachmentStorage>(sp => sp.GetRequiredService<FileSystemAttemptAttachmentStorage>());
        }

        services.AddHostedService<AttachmentOrphanCleanupWorker>();
        return services;
    }

    private static bool ShouldUseDatabaseKeyRing(
        AttachmentStorageOptions options,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var connString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connString))
        {
            return false;
        }

        if (string.Equals(options.DataProtectionKeyStorage, "Database", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(options.DataProtectionKeyStorage, "FileSystem", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Auto: use MySQL key ring in Staging/Production when no persistent filesystem path is configured
        return !isDevelopment;
    }
}
