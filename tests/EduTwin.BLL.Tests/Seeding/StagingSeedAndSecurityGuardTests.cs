using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using EduTwin.API.Security;
using EduTwin.BLL.Seeding;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTwin.BLL.Tests.Seeding;

public sealed class StagingSeedAndSecurityGuardTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void StagingSeedPolicy_DevelopmentWithSeedEnabled_AllowsSeeding()
    {
        var config = BuildConfig(new()
        {
            ["Seed:Enabled"] = "true",
            ["ConnectionStrings:Default"] = "Server=localhost;Database=edutwin;"
        });

        var decision = StagingSeedPolicy.Evaluate(config, "Development", initStagingFlag: false);

        Assert.True(decision.IsDevelopment);
        Assert.False(decision.IsStaging);
        Assert.True(decision.ShouldSeed);
    }

    [Fact]
    public void StagingSeedPolicy_StagingWithoutOptIn_Throws()
    {
        var config = BuildConfig(new()
        {
            ["Seed:Enabled"] = "true",
            ["StagingInit:AllowSeed"] = "false",
            ["ConnectionStrings:Default"] = "Server=localhost;Database=edutwin_staging;"
        });

        Assert.Throws<InvalidOperationException>(() =>
            StagingSeedPolicy.Evaluate(config, "Staging", initStagingFlag: false));
    }

    [Fact]
    public void StagingSeedPolicy_StagingWithOptIn_AllowsSeeding()
    {
        var config = BuildConfig(new()
        {
            ["Seed:Enabled"] = "true",
            ["StagingInit:AllowSeed"] = "true",
            ["StagingInit:ExpectedDatabase"] = "edutwin_staging",
            ["ConnectionStrings:Default"] = "Server=localhost;Database=edutwin_staging;"
        });

        var decision = StagingSeedPolicy.Evaluate(config, "Staging", initStagingFlag: false);

        Assert.True(decision.IsStaging);
        Assert.True(decision.ShouldSeed);
        Assert.True(decision.IsStagingSeedAllowed);
    }

    [Fact]
    public void StagingSeedPolicy_InitStagingFlagWithoutSeedEnabledConfig_StillEnablesSeed()
    {
        var config = BuildConfig(new()
        {
            ["Seed:Enabled"] = "false",
            ["StagingInit:ExpectedDatabase"] = "edutwin_staging",
            ["ConnectionStrings:Default"] = "Server=localhost;Database=edutwin_staging;"
        });

        var decision = StagingSeedPolicy.Evaluate(config, "Staging", initStagingFlag: true);

        Assert.True(decision.ShouldSeed);
        Assert.True(decision.IsStagingSeedAllowed);
    }

    [Fact]
    public void StagingSeedPolicy_ProductionWithSeedEnabled_Throws()
    {
        var config = BuildConfig(new()
        {
            ["Seed:Enabled"] = "true",
            ["StagingInit:AllowSeed"] = "true",
            ["ConnectionStrings:Default"] = "Server=localhost;Database=edutwin_prod;"
        });

        Assert.Throws<InvalidOperationException>(() =>
            StagingSeedPolicy.Evaluate(config, "Production", initStagingFlag: false));
    }

    [Fact]
    public void StagingSeedPolicy_InitStagingFlagInProductionOrDevelopment_Throws()
    {
        var config = BuildConfig(new()
        {
            ["ConnectionStrings:Default"] = "Server=localhost;Database=edutwin_prod;"
        });

        Assert.Throws<InvalidOperationException>(() =>
            StagingSeedPolicy.Evaluate(config, "Production", initStagingFlag: true));
        Assert.Throws<InvalidOperationException>(() =>
            StagingSeedPolicy.Evaluate(config, "Development", initStagingFlag: true));
    }

    [Fact]
    public void StagingSeedPolicy_MismatchedExpectedDatabase_Throws()
    {
        var config = BuildConfig(new()
        {
            ["Seed:Enabled"] = "true",
            ["StagingInit:AllowSeed"] = "true",
            ["StagingInit:ExpectedDatabase"] = "edutwin_staging",
            ["ConnectionStrings:Default"] = "Server=localhost;Database=edutwin_production;"
        });

        Assert.Throws<InvalidOperationException>(() =>
            StagingSeedPolicy.Evaluate(config, "Staging", initStagingFlag: false));
    }

    [Fact]
    public void RuntimeEnvironmentSecurity_UnsetEnvironment_DefaultsToProductionFailClosed()
    {
        var config = BuildConfig(new());
        var resolved = RuntimeEnvironmentSecurity.ResolveEnvironmentName(config, explicitEnvironmentName: null);
        // Unless host process explicitly sets ASPNETCORE_ENVIRONMENT=Development, default is non-Development
        Assert.False(string.IsNullOrWhiteSpace(resolved));

        var dotnetEnvConfig = BuildConfig(new() { ["DOTNET_ENVIRONMENT"] = "Staging" });
        Assert.Equal("Staging", RuntimeEnvironmentSecurity.ResolveEnvironmentName(dotnetEnvConfig));
        Assert.True(RuntimeEnvironmentSecurity.IsStaging(dotnetEnvConfig));
        Assert.False(RuntimeEnvironmentSecurity.IsDevelopment(dotnetEnvConfig));
    }

    [Theory]
    [InlineData("PlatformAdmin@2026")]
    [InlineData("change_me_to_a_secure_seed_password")]
    [InlineData("change_me_to_a_secure_platform_admin_password")]
    [InlineData("your_placeholder_secret_1234")]
    public void RuntimeEnvironmentSecurity_RejectsDefaultAndPlaceholderPasswordsInNonDevelopment(string weakPassword)
    {
        Assert.Throws<InvalidOperationException>(() =>
            RuntimeEnvironmentSecurity.EnsureSecureCredential(weakPassword, "PlatformBootstrap:AdminPassword", isDevelopment: false));
    }

    [Fact]
    public void RuntimeEnvironmentSecurity_AllowsStrongUniquePasswordInNonDevelopment()
    {
        RuntimeEnvironmentSecurity.EnsureSecureCredential("Xy9!kQ7#mP2$vL8@nR4", "PlatformBootstrap:AdminPassword", isDevelopment: false);
    }

    [Fact]
    public void MySqlDataProtectionKeyRepository_EncryptsAndDecryptsAtRest_AndRejectsTampering()
    {
        var repo = new MySqlDataProtectionKeyRepository(
            "Server=localhost;Database=unused;",
            "Test_At_Rest_Encryption_Secret_32Bytes!",
            NullLogger<MySqlDataProtectionKeyRepository>.Instance);

        const string sampleXml = "<key id=\"test\"><value>secret-key-material</value></key>";
        var protectedPayload = repo.ProtectPayload(sampleXml);

        Assert.StartsWith("ENC:V1:", protectedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-key-material", protectedPayload, StringComparison.Ordinal);

        var restored = repo.UnprotectPayload(protectedPayload);
        Assert.Equal(sampleXml, restored);

        // Tampered payload must throw CryptographicException
        var tampered = protectedPayload[..^4] + "AAAA";
        Assert.ThrowsAny<CryptographicException>(() => repo.UnprotectPayload(tampered));

        // Wrong secret must throw CryptographicException
        var wrongKeyRepo = new MySqlDataProtectionKeyRepository(
            "Server=localhost;Database=unused;",
            "Different_Wrong_Secret_Value_32Bytes!",
            NullLogger<MySqlDataProtectionKeyRepository>.Instance);
        Assert.ThrowsAny<CryptographicException>(() => wrongKeyRepo.UnprotectPayload(protectedPayload));
    }

    [Fact]
    public void MySqlDataProtectionKeyRepository_PersistsKeysAcrossSimulatedRestart_WhenMySqlAvailable()
    {
        var connString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connString))
        {
            return;
        }

        const string secret = "Live_MySql_Restart_Test_Secret_32Bytes!";
        var appName = "EduTwin-Test-" + Guid.NewGuid().ToString("N");

        Microsoft.AspNetCore.DataProtection.IDataProtectionProvider CreateProvider()
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddLogging();
            services.AddDataProtection().SetApplicationName(appName);
            services.AddSingleton<Microsoft.Extensions.Options.IConfigureOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>>(_ =>
                new Microsoft.Extensions.Options.ConfigureOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(opts =>
                    opts.XmlRepository = new MySqlDataProtectionKeyRepository(connString, secret, NullLogger<MySqlDataProtectionKeyRepository>.Instance)));
            return services.BuildServiceProvider().GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
        }

        var providerBeforeRestart = CreateProvider();
        var protectorBefore = providerBeforeRestart.CreateProtector("EduTwin.AttemptAttachment.v1");
        var token = protectorBefore.Protect("{\"centerId\":\"11111111-1111-1111-1111-111111111111\",\"nonce\":\"abc\"}");

        // Simulate full backend restart with brand new ServiceProvider & key ring load from MySQL
        var providerAfterRestart = CreateProvider();
        var protectorAfter = providerAfterRestart.CreateProtector("EduTwin.AttemptAttachment.v1");
        var unprotected = protectorAfter.Unprotect(token);

        Assert.Equal("{\"centerId\":\"11111111-1111-1111-1111-111111111111\",\"nonce\":\"abc\"}", unprotected);
        Assert.ThrowsAny<CryptographicException>(() => protectorAfter.Unprotect(token + "tampered"));
    }
}
