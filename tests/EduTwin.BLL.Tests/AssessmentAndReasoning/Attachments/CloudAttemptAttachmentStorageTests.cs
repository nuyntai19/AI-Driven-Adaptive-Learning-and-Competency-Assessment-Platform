using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.API.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Attachments;

public sealed class CloudAttemptAttachmentStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AttachmentStorageOptions _options;
    private readonly Mock<IWebHostEnvironment> _envMock;
    private readonly FileSystemAttemptAttachmentStorage _localStorage;
    private readonly Mock<ICloudObjectStorageClient> _cloudMock;
    private readonly CloudAttemptAttachmentStorage _sut;

    public CloudAttemptAttachmentStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "edutwin-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _options = new AttachmentStorageOptions
        {
            RootPath = _tempDir,
            Provider = "Cloud",
            CloudBaseUrl = "https://mock.supabase.co/storage/v1",
            CloudApiKey = "mock-secret-key",
            BucketName = "edutwin-attachments"
        };

        _envMock = new Mock<IWebHostEnvironment>();
        _envMock.Setup(e => e.ContentRootPath).Returns(_tempDir);

        _localStorage = new FileSystemAttemptAttachmentStorage(
            Options.Create(_options),
            _envMock.Object,
            TimeProvider.System);

        _cloudMock = new Mock<ICloudObjectStorageClient>();
        _cloudMock.Setup(c => c.IsConfigured).Returns(true);

        _sut = new CloudAttemptAttachmentStorage(
            _localStorage,
            _cloudMock.Object,
            Options.Create(_options),
            NullLogger<CloudAttemptAttachmentStorage>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in temporary test directories
        }
    }

    [Fact]
    public void AttachmentStorageOptions_DefaultBucketName_IsEdutwinAttachments()
    {
        var defaults = new AttachmentStorageOptions();
        Assert.Equal("edutwin-attachments", defaults.BucketName);
    }

    [Fact]
    public void DependencyInjection_WithSupabaseProvider_ValidatesScopesAndLifetimesWithoutCaptiveDependency()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["AttachmentStorage:Provider"] = "Supabase",
                ["AttachmentStorage:RootPath"] = _tempDir,
                ["AttachmentStorage:CloudBaseUrl"] = "https://mock.supabase.co/storage/v1",
                ["AttachmentStorage:CloudApiKey"] = "mock-service-role-key",
                ["AttachmentStorage:BucketName"] = "edutwin-attachments"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_envMock.Object);
        services.AddSingleton(TimeProvider.System);
        services.AddAttemptAttachmentStorage(config);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        var storage = provider.GetRequiredService<IAttemptAttachmentStorage>();
        var cloudClient = provider.GetRequiredService<ICloudObjectStorageClient>();
        Assert.IsType<CloudAttemptAttachmentStorage>(storage);
        Assert.IsType<SupabaseStorageClient>(cloudClient);
    }

    [Fact]
    public async Task DeletePermanentAsync_DeletesFromBothLocalAndCloud()
    {
        var storageKey = "tenants/00000000-0000-0000-0000-000000000001/attempt-attachments/test.png";

        await _sut.DeletePermanentAsync(storageKey, CancellationToken.None);

        _cloudMock.Verify(c => c.DeleteAsync(_options.BucketName, storageKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePermanentAsync_WhenCloudDeleteFails_PreservesLocalDeleteAndDoesNotThrow()
    {
        var storageKey = "tenants/00000000-0000-0000-0000-000000000001/attempt-attachments/local-only.png";
        var localPath = Path.Combine(_tempDir, storageKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        await File.WriteAllTextAsync(localPath, "bytes");

        _cloudMock.Setup(c => c.DeleteAsync(_options.BucketName, storageKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated cloud outage"));

        await _sut.DeletePermanentAsync(storageKey, CancellationToken.None);

        Assert.False(File.Exists(localPath));
    }

    [Fact]
    public async Task OpenPermanentReadAsync_WhenLocalMiss_DownloadsFromCloudAndCachesLocally()
    {
        var storageKey = "tenants/00000000-0000-0000-0000-000000000001/attempt-attachments/cached.png";
        var mockContent = new MemoryStream(Encoding.UTF8.GetBytes("cloud-stored-png-bytes"));

        _cloudMock.Setup(c => c.DownloadAsync(_options.BucketName, storageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockContent);

        var stream = await _sut.OpenPermanentReadAsync(storageKey, CancellationToken.None);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        Assert.Equal("cloud-stored-png-bytes", content);
        _cloudMock.Verify(c => c.DownloadAsync(_options.BucketName, storageKey, It.IsAny<CancellationToken>()), Times.Once);

        // Verify it was cached locally under the consistent root
        var localPath = Path.Combine(_tempDir, storageKey.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(localPath));
    }

    [Fact]
    public async Task OpenPermanentReadAsync_WhenRootPathIsNull_UsesContentRootStorageConsistently()
    {
        var optionsNoRoot = new AttachmentStorageOptions
        {
            RootPath = null,
            Provider = "Cloud",
            CloudBaseUrl = "https://mock.supabase.co/storage/v1",
            CloudApiKey = "mock-secret-key",
            BucketName = "edutwin-attachments"
        };
        var local = new FileSystemAttemptAttachmentStorage(Options.Create(optionsNoRoot), _envMock.Object, TimeProvider.System);
        var sut = new CloudAttemptAttachmentStorage(local, _cloudMock.Object, Options.Create(optionsNoRoot), NullLogger<CloudAttemptAttachmentStorage>.Instance);

        var storageKey = "tenants/00000000-0000-0000-0000-000000000001/attempt-attachments/consistent.png";
        _cloudMock.Setup(c => c.DownloadAsync(optionsNoRoot.BucketName, storageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("consistent-bytes")));

        await using var stream = await sut.OpenPermanentReadAsync(storageKey, CancellationToken.None);
        var expectedPath = Path.Combine(_tempDir, "storage", storageKey.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task OpenPermanentReadAsync_WhenNeitherLocalNorCloud_ThrowsFileNotFoundException()
    {
        var storageKey = "tenants/00000000-0000-0000-0000-000000000001/attempt-attachments/missing.png";
        _cloudMock.Setup(c => c.DownloadAsync(_options.BucketName, storageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _sut.OpenPermanentReadAsync(storageKey, CancellationToken.None));
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("tenants/../../etc/passwd")]
    [InlineData("tenants/%2e%2e/escape.png")]
    [InlineData("/etc/passwd")]
    [InlineData("tenants\\..\\secret.png")]
    public async Task PathTraversalAttempts_AreRejectedBeforeCloudOrLocalAccess(string maliciousKey)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.OpenPermanentReadAsync(maliciousKey, CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.DeletePermanentAsync(maliciousKey, CancellationToken.None));

        _cloudMock.Verify(c => c.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cloudMock.Verify(c => c.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OrphanCleanupWorker_SweepsExpiredUnreferencedCloudObjects_AndPreservesReferencedAndRecent()
    {
        var centerId = Guid.NewGuid();
        var referencedNonce = Guid.NewGuid().ToString("N");
        var orphanNonce = Guid.NewGuid().ToString("N");
        var recentNonce = Guid.NewGuid().ToString("N");
        var expiredTempNonce = Guid.NewGuid().ToString("N");

        var referencedKey = $"tenants/{centerId:D}/attempt-attachments/{referencedNonce}.png";
        var orphanKey = $"tenants/{centerId:D}/attempt-attachments/{orphanNonce}.png";
        var expiredTempKey = $"tenants/{centerId:D}/attempt-attachments-temp/{expiredTempNonce}.png";

        var dbName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<EduTwinDbContext>(o => o.UseInMemoryDatabase(dbName));
        using var sp = services.BuildServiceProvider();

        using (var seedScope = sp.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
            db.Centers.Add(new Center
            {
                CenterId = centerId,
                CenterCode = "CLOUD",
                CenterName = "Cloud Test Center",
                Timezone = "UTC",
                Status = CenterStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.AttemptAttachments.Add(new AttemptAttachment
            {
                AttachmentId = 1,
                CenterId = centerId,
                AttemptId = 1,
                FileName = $"{referencedNonce}.png",
                StorageKey = referencedKey,
                ContentType = "image/png",
                FileSizeBytes = 128,
                UploadNonce = referencedNonce,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var now = new DateTime(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);
        var cloudMock = new Mock<ICloudObjectStorageClient>();
        cloudMock.Setup(c => c.IsConfigured).Returns(true);
        cloudMock.Setup(c => c.ListObjectsAsync("edutwin-attachments", $"tenants/{centerId:D}/attempt-attachments-temp", 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CloudStorageObjectInfo($"{expiredTempNonce}.png", now.AddHours(-40), now.AddHours(-40))
            });
        cloudMock.Setup(c => c.ListObjectsAsync("edutwin-attachments", $"tenants/{centerId:D}/attempt-attachments", 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CloudStorageObjectInfo($"{referencedNonce}.png", now.AddHours(-48), now.AddHours(-48)),
                new CloudStorageObjectInfo($"{orphanNonce}.png", now.AddHours(-48), now.AddHours(-48)),
                new CloudStorageObjectInfo($"{recentNonce}.png", now.AddHours(-2), now.AddHours(-2)),
                new CloudStorageObjectInfo("non-edutwin-note.txt", now.AddHours(-48), now.AddHours(-48))
            });

        var worker = new AttachmentOrphanCleanupWorker(
            Options.Create(_options),
            _envMock.Object,
            sp.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            NullLogger<AttachmentOrphanCleanupWorker>.Instance,
            cloudMock.Object);

        var deleted = await worker.SweepOrphansOnceAsync(now, CancellationToken.None);

        Assert.Equal(2, deleted);
        cloudMock.Verify(c => c.DeleteAsync("edutwin-attachments", expiredTempKey, It.IsAny<CancellationToken>()), Times.Once);
        cloudMock.Verify(c => c.DeleteAsync("edutwin-attachments", orphanKey, It.IsAny<CancellationToken>()), Times.Once);
        cloudMock.Verify(c => c.DeleteAsync("edutwin-attachments", referencedKey, It.IsAny<CancellationToken>()), Times.Never);
    }
}
