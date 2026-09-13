using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.API.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MySql.Data.MySqlClient;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Attachments;

[Collection("MySqlDatabase")]
public sealed class AttachmentOrphanCleanupWorkerMySqlIntegrationTests
{
    private const string AdminConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";

    [MySqlIntegrationFact]
    public async Task SweepOrphansOnceAsync_MultiTenantLiveMySql_PreservesReferencedBlobsAcrossTenantsAndPurgesUnreferencedOrphan()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerA = Guid.NewGuid();
        var centerB = Guid.NewGuid();
        var nonceA = Guid.NewGuid().ToString("N");
        var nonceB = Guid.NewGuid().ToString("N");
        var orphanNonceA = Guid.NewGuid().ToString("N");
        var freshNonceB = Guid.NewGuid().ToString("N");

        var storageRoot = Path.Combine(Path.GetTempPath(), "edutwin-mysql-sweeper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storageRoot);

        try
        {
            var now = DateTime.UtcNow;

            // 1. Filesystem setup
            var permDirA = Path.Combine(storageRoot, "tenants", centerA.ToString("D"), "attempt-attachments");
            var permDirB = Path.Combine(storageRoot, "tenants", centerB.ToString("D"), "attempt-attachments");
            Directory.CreateDirectory(permDirA);
            Directory.CreateDirectory(permDirB);

            // File 1: Tenant A referenced permanent file (48h old) -> MUST BE PRESERVED
            var fileA = Path.Combine(permDirA, $"{nonceA}.png");
            await File.WriteAllBytesAsync(fileA, [1, 2, 3]);
            File.SetLastWriteTimeUtc(fileA, now.AddHours(-48));

            // File 2: Tenant B referenced permanent file (48h old) -> MUST BE PRESERVED
            var fileB = Path.Combine(permDirB, $"{nonceB}.png");
            await File.WriteAllBytesAsync(fileB, [4, 5, 6]);
            File.SetLastWriteTimeUtc(fileB, now.AddHours(-48));

            // File 3: Tenant A unreferenced orphan permanent file (48h old) -> MUST BE PURGED
            var orphanFileA = Path.Combine(permDirA, $"{orphanNonceA}.png");
            await File.WriteAllBytesAsync(orphanFileA, [7, 8, 9]);
            File.SetLastWriteTimeUtc(orphanFileA, now.AddHours(-48));

            // File 4: Tenant B fresh unreferenced permanent file (10m old) -> MUST BE PRESERVED (grace period)
            var freshFileB = Path.Combine(permDirB, $"{freshNonceB}.png");
            await File.WriteAllBytesAsync(freshFileB, [10, 11, 12]);
            File.SetLastWriteTimeUtc(freshFileB, now.AddMinutes(-10));

            // 2. MySQL database setup: Seed Center A and Center B and their referenced AttemptAttachment records
            var keyA = $"tenants/{centerA:D}/attempt-attachments/{nonceA}.png";
            var keyB = $"tenants/{centerB:D}/attempt-attachments/{nonceB}.png";

            await using (var conn = new MySqlConnection(database.ConnectionString))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SET FOREIGN_KEY_CHECKS = 0;
                    INSERT INTO centers (center_id, center_code, center_name, status, created_at, updated_at)
                    VALUES (@centerA, 'SWEEP-A', 'Center A Sweep', 'Active', NOW(), NOW()),
                           (@centerB, 'SWEEP-B', 'Center B Sweep', 'Active', NOW(), NOW());
                    INSERT INTO attempt_attachments (center_id, attempt_id, upload_nonce, file_name, content_type, storage_key, file_size_bytes, created_at)
                    VALUES (@centerA, 1, @nonceA, 'drawingA.png', 'image/png', @keyA, 3, NOW()),
                           (@centerB, 2, @nonceB, 'drawingB.png', 'image/png', @keyB, 3, NOW());
                    SET FOREIGN_KEY_CHECKS = 1;
                ";
                cmd.Parameters.AddWithValue("@centerA", centerA.ToString("D"));
                cmd.Parameters.AddWithValue("@centerB", centerB.ToString("D"));
                cmd.Parameters.AddWithValue("@nonceA", nonceA);
                cmd.Parameters.AddWithValue("@nonceB", nonceB);
                cmd.Parameters.AddWithValue("@keyA", keyA);
                cmd.Parameters.AddWithValue("@keyB", keyB);
                await cmd.ExecuteNonQueryAsync();
            }

            // 3. Configure DI container connected to live MySQL
            var services = new ServiceCollection();
            services.AddDbContext<EduTwinDbContext>(opt => opt.UseMySQL(database.ConnectionString));
            services.AddScoped<ITenantIdAccessor, TenantContext>();
            await using var serviceProvider = services.BuildServiceProvider();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            var options = Options.Create(new AttachmentStorageOptions
            {
                RootPath = storageRoot,
                GracePeriodHours = 24,
                CleanupIntervalMinutes = 60,
            });

            var envMock = new Mock<IWebHostEnvironment>();
            envMock.SetupGet(e => e.ContentRootPath).Returns(storageRoot);

            var worker = new AttachmentOrphanCleanupWorker(
                options,
                envMock.Object,
                scopeFactory,
                TimeProvider.System,
                NullLogger<AttachmentOrphanCleanupWorker>.Instance);

            // 4. Act: Execute sweeper against live MySQL database
            var deletedCount = await worker.SweepOrphansOnceAsync(now, CancellationToken.None);

            // 5. Assert
            Assert.Equal(1, deletedCount);

            // Tenant A referenced file must survive
            Assert.True(File.Exists(fileA), "Tenant A referenced file must survive sweeper.");

            // Tenant B referenced file must survive (verifying cross-tenant IgnoreQueryFilters on real MySQL)
            Assert.True(File.Exists(fileB), "Tenant B referenced file must survive sweeper across tenants.");

            // Tenant A unreferenced orphan must be deleted
            Assert.False(File.Exists(orphanFileA), "Tenant A unreferenced orphan older than 24h must be purged.");

            // Tenant B fresh unreferenced file must survive within grace period
            Assert.True(File.Exists(freshFileB), "Tenant B fresh unreferenced file within grace period must survive.");
        }
        finally
        {
            try
            {
                if (Directory.Exists(storageRoot))
                {
                    Directory.Delete(storageRoot, true);
                }
            }
            catch
            {
                // Ignore cleanup error
            }
        }
    }

    private sealed class MySqlIntegrationFactAttribute : FactAttribute
    {
        public MySqlIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AdminConnectionVariable)))
            {
                Skip = $"Set {AdminConnectionVariable} to run MySQL integration tests.";
            }
        }
    }

    private sealed class MySqlTestDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _databaseName;

        private MySqlTestDatabase(string adminConnectionString, string databaseName, string connectionString)
        {
            _adminConnectionString = adminConnectionString;
            _databaseName = databaseName;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<MySqlTestDatabase> CreateAsync()
        {
            var configuredConnection = Environment.GetEnvironmentVariable(AdminConnectionVariable)
                ?? throw new InvalidOperationException($"{AdminConnectionVariable} is required.");
            var adminBuilder = new MySqlConnectionStringBuilder(configuredConnection)
            {
                Database = string.Empty,
                Pooling = false
            };
            var databaseName = $"edutwin_sweep_{Guid.NewGuid():N}";
            await using (var connection = new MySqlConnection(adminBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4;";
                await command.ExecuteNonQueryAsync();
            }

            var databaseBuilder = new MySqlConnectionStringBuilder(adminBuilder.ConnectionString)
            {
                Database = databaseName,
                Pooling = false
            };
            var database = new MySqlTestDatabase(adminBuilder.ConnectionString, databaseName, databaseBuilder.ConnectionString);
            try
            {
                var tenant = new TenantContext();
                var options = new DbContextOptionsBuilder<EduTwinDbContext>()
                    .UseMySQL(database.ConnectionString)
                    .Options;
                await using var context = new EduTwinDbContext(options, tenant);
                await context.Database.MigrateAsync();
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new MySqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`;";
            await command.ExecuteNonQueryAsync();
        }
    }
}
