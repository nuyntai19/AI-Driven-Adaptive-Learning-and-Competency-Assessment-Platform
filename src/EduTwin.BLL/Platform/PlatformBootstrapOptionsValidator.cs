using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace EduTwin.BLL.Platform;

public sealed class PlatformBootstrapOptionsValidator(IConfiguration configuration) : IValidateOptions<PlatformBootstrapOptions>
{
    public ValidateOptionsResult Validate(string? name, PlatformBootstrapOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("PlatformBootstrap configuration section is missing.");
        }

        if (string.IsNullOrWhiteSpace(options.AdminUsername))
        {
            return ValidateOptionsResult.Fail("PlatformBootstrap:AdminUsername is required.");
        }

        if (string.IsNullOrWhiteSpace(options.AdminDisplayName))
        {
            return ValidateOptionsResult.Fail("PlatformBootstrap:AdminDisplayName is required.");
        }

        var env = configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        bool isDevelopment = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);

        // In non-development environments, AdminPassword must be explicitly configured
        if (!isDevelopment && string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            return ValidateOptionsResult.Fail("PlatformBootstrap:AdminPassword is required in non-Development environments.");
        }

        return ValidateOptionsResult.Success;
    }
}
