using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

namespace EduTwin.BLL.Seeding;

/// <summary>
/// Fail-closed runtime environment and credential guard shared by bootstrap and seeding flows.
/// </summary>
public static class RuntimeEnvironmentSecurity
{
    private static readonly HashSet<string> BlockedDefaultPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "PlatformAdmin@2026",
        "CenterManager@2026",
        "Teacher@2026",
        "Student@2026",
        "EduTwin@2026",
        "change_me_to_a_secure_platform_admin_password",
        "change_me_to_a_secure_seed_password",
        "edutwin_password",
        "root_password"
    };

    /// <summary>
    /// Resolves the active runtime environment. Defaults to "Production" (fail-closed) when unset.
    /// </summary>
    public static string ResolveEnvironmentName(IConfiguration? configuration = null, string? explicitEnvironmentName = null)
    {
        var candidate = !string.IsNullOrWhiteSpace(explicitEnvironmentName)
            ? explicitEnvironmentName
            : configuration?["ASPNETCORE_ENVIRONMENT"]
              ?? configuration?["DOTNET_ENVIRONMENT"]
              ?? configuration?["ENVIRONMENT"]
              ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
              ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        return string.IsNullOrWhiteSpace(candidate) ? "Production" : candidate.Trim();
    }

    public static bool IsDevelopment(IConfiguration? configuration = null, string? explicitEnvironmentName = null) =>
        string.Equals(ResolveEnvironmentName(configuration, explicitEnvironmentName), "Development", StringComparison.OrdinalIgnoreCase);

    public static bool IsStaging(IConfiguration? configuration = null, string? explicitEnvironmentName = null) =>
        string.Equals(ResolveEnvironmentName(configuration, explicitEnvironmentName), "Staging", StringComparison.OrdinalIgnoreCase);

    public static void EnsureSecureCredential([System.Diagnostics.CodeAnalysis.NotNull] string? password, string settingName, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new InvalidOperationException(
                $"Missing or invalid {settingName} configuration (must be at least 12 characters).");
        }

        if (isDevelopment)
        {
            return;
        }

        var trimmed = password.Trim();
        if (BlockedDefaultPasswords.Contains(trimmed) ||
            trimmed.Contains("change_me", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("your_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"CRITICAL SECURITY ERROR: Default or placeholder password cannot be used for {settingName} in a non-Development environment.");
        }
    }
}

public sealed record StagingSeedDecision(
    string EnvironmentName,
    bool IsDevelopment,
    bool IsStaging,
    bool ShouldSeed,
    bool IsStagingSeedAllowed);

public static class StagingSeedPolicy
{
    public static StagingSeedDecision Evaluate(
        IConfiguration configuration,
        string environmentName,
        bool initStagingFlag)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var resolvedEnv = RuntimeEnvironmentSecurity.ResolveEnvironmentName(configuration, environmentName);
        var isDev = string.Equals(resolvedEnv, "Development", StringComparison.OrdinalIgnoreCase);
        var isStaging = string.Equals(resolvedEnv, "Staging", StringComparison.OrdinalIgnoreCase);

        if (initStagingFlag && !isStaging)
        {
            throw new InvalidOperationException(
                $"CRITICAL SECURITY ERROR: --init-staging is only permitted when runtime environment is Staging (actual: '{resolvedEnv}').");
        }

        var allowStagingSeed = configuration.GetValue<bool>("StagingInit:AllowSeed") || initStagingFlag;
        var seedEnabled = configuration.GetValue<bool>("Seed:Enabled") || initStagingFlag;

        if (seedEnabled && !isDev && !(isStaging && allowStagingSeed))
        {
            throw new InvalidOperationException(
                "CRITICAL SECURITY ERROR: Seeding is enabled in a non-Development environment without explicit Staging opt-in! Shutting down to protect data.");
        }

        if (isStaging && seedEnabled)
        {
            ValidateStagingTargetDatabase(configuration);
        }

        return new StagingSeedDecision(
            EnvironmentName: resolvedEnv,
            IsDevelopment: isDev,
            IsStaging: isStaging,
            ShouldSeed: seedEnabled,
            IsStagingSeedAllowed: isStaging && allowStagingSeed);
    }

    public static void ValidateStagingTargetDatabase(IConfiguration configuration)
    {
        var expectedDb = configuration["StagingInit:ExpectedDatabase"];
        var connString = configuration.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(connString))
        {
            throw new InvalidOperationException(
                "CRITICAL SECURITY ERROR: ConnectionStrings:Default is required for Staging initialization.");
        }

        if (!string.IsNullOrWhiteSpace(expectedDb))
        {
            var builder = new MySqlConnectionStringBuilder(connString);
            if (!string.Equals(builder.Database, expectedDb.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"CRITICAL SECURITY ERROR: Target database '{builder.Database}' does not match StagingInit:ExpectedDatabase '{expectedDb}'. Aborting to prevent unintended writes.");
            }
        }
    }
}
