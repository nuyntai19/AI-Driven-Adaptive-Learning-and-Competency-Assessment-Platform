using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.API.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Attachments;

public sealed class AttachmentOrphanCleanupWorkerTests : IDisposable
{
    private readonly string _testRoot;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AttachmentOrphanCleanupWorker _worker;
    private readonly IOptions<AttachmentStorageOptions> _options;
    private readonly Mock<IWebHostEnvironment> _envMock;

    public AttachmentOrphanCleanupWorkerTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "edutwin-test-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        _options = Options.Create(new AttachmentStorageOptions
        {
            RootPath = _testRoot,
            GracePeriodHours = 24,
            CleanupIntervalMinutes = 60,
        });

        _envMock = new Mock<IWebHostEnvironment>();
        _envMock.SetupGet(e => e.ContentRootPath).Returns(_testRoot);

        var dbName = "edutwin-orphan-test-" + Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<EduTwinDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        _serviceProvider = services.BuildServiceProvider();
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _worker = new AttachmentOrphanCleanupWorker(
            _options,
            _envMock.Object,
            _scopeFactory,
            TimeProvider.System,
            NullLogger<AttachmentOrphanCleanupWorker>.Instance);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }
        catch
        {
            // Ignore test cleanup errors
        }
    }

    [Fact]
    public void SweepOrphansOnce_WhenNoTenantsExist_ReturnsZeroWithoutError()
    {
        var deletedCount = _worker.SweepOrphansOnce(DateTime.UtcNow, CancellationToken.None);
        Assert.Equal(0, deletedCount);
    }

    [Fact]
    public async Task SweepOrphansOnce_DeletesExpiredTempPng_AndPreservesRecentAndPermanentFiles()
    {
        var tenantId = Guid.NewGuid();
        var tempDir = Path.Combine(_testRoot, "tenants", tenantId.ToString("D"), "attempt-attachments-temp");
        var permDir = Path.Combine(_testRoot, "tenants", tenantId.ToString("D"), "attempt-attachments");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(permDir);

        var now = DateTime.UtcNow;

        // 1. Expired temp png (>26h old temp grace period) -> should be deleted
        var expiredTempFile = Path.Combine(tempDir, "expired.png");
        File.WriteAllBytes(expiredTempFile, [1, 2, 3]);
        File.SetLastWriteTimeUtc(expiredTempFile, now.AddHours(-30));

        // 2. Fresh temp png (<24h old) -> should be kept
        var recentTempFile = Path.Combine(tempDir, "recent.png");
        File.WriteAllBytes(recentTempFile, [4, 5, 6]);
        File.SetLastWriteTimeUtc(recentTempFile, now.AddHours(-2));

        // 3. Non-PNG file in temp folder -> should be ignored/kept
        var textFile = Path.Combine(tempDir, "notes.txt");
        File.WriteAllText(textFile, "keep me");
        File.SetLastWriteTimeUtc(textFile, now.AddHours(-30));

        // 4. File in permanent folder referenced in DB -> preserved
        var nonce = Guid.NewGuid().ToString("N");
        var permFile = Path.Combine(permDir, $"{nonce}.png");
        File.WriteAllBytes(permFile, [7, 8, 9]);
        File.SetLastWriteTimeUtc(permFile, now.AddHours(-48));

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
            db.AttemptAttachments.Add(new AttemptAttachment
            {
                CenterId = tenantId,
                AttemptId = 1,
                UploadNonce = nonce,
                FileName = "drawing.png",
                ContentType = "image/png",
                StorageKey = $"tenants/{tenantId:D}/attempt-attachments/{nonce}.png",
                FileSizeBytes = 3,
                CreatedAt = now.AddHours(-48)
            });
            await db.SaveChangesAsync();
        }

        using (var verifyScope = _scopeFactory.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
            var saved = await db.AttemptAttachments.IgnoreQueryFilters().ToListAsync();
            Assert.Single(saved);
        }

        var deletedCount = await _worker.SweepOrphansOnceAsync(now, CancellationToken.None);

        Assert.Equal(1, deletedCount);
        Assert.False(File.Exists(expiredTempFile), "Expired temporary attachment must be deleted.");
        Assert.True(File.Exists(recentTempFile), "Recent temporary attachment must be preserved.");
        Assert.True(File.Exists(textFile), "Non-png files in temp folder should not be deleted by worker.");
        Assert.True(File.Exists(permFile), "Permanent attachment with valid DB reference must be preserved.");
    }

    [Fact]
    public async Task SweepOrphansOnceAsync_SimulateCrashBetweenPromoteAndCommit_PurgesExpiredPermanentOrphanAndPreservesValidFiles()
    {
        var tenantId = Guid.NewGuid();
        var tempDir = Path.Combine(_testRoot, "tenants", tenantId.ToString("D"), "attempt-attachments-temp");
        var permDir = Path.Combine(_testRoot, "tenants", tenantId.ToString("D"), "attempt-attachments");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(permDir);

        var now = DateTime.UtcNow;

        // Case 1: Permanent file with DB reference, age 48h -> MUST BE PRESERVED
        var committedNonce = Guid.NewGuid().ToString("N");
        var committedFile = Path.Combine(permDir, $"{committedNonce}.png");
        File.WriteAllBytes(committedFile, [10, 20]);
        File.SetLastWriteTimeUtc(committedFile, now.AddHours(-48));

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
            db.AttemptAttachments.Add(new AttemptAttachment
            {
                CenterId = tenantId,
                AttemptId = 100,
                UploadNonce = committedNonce,
                FileName = "valid_drawing.png",
                ContentType = "image/png",
                StorageKey = $"tenants/{tenantId:D}/attempt-attachments/{committedNonce}.png",
                FileSizeBytes = 2,
                CreatedAt = now.AddHours(-48)
            });
            await db.SaveChangesAsync();
        }

        // Case 2: Permanent file from crash between promote and commit, age 48h (unreferenced) -> MUST BE PURGED
        var crashedExpiredNonce = Guid.NewGuid().ToString("N");
        var crashedExpiredFile = Path.Combine(permDir, $"{crashedExpiredNonce}.png");
        File.WriteAllBytes(crashedExpiredFile, [30, 40]);
        File.SetLastWriteTimeUtc(crashedExpiredFile, now.AddHours(-48));

        // Case 3: Permanent file recently promoted (e.g. 10m ago), unreferenced (in-flight or recent crash) -> MUST BE PRESERVED (grace period)
        var crashedRecentNonce = Guid.NewGuid().ToString("N");
        var crashedRecentFile = Path.Combine(permDir, $"{crashedRecentNonce}.png");
        File.WriteAllBytes(crashedRecentFile, [50, 60]);
        File.SetLastWriteTimeUtc(crashedRecentFile, now.AddMinutes(-10));

        // Case 4: Temporary file, age 48h -> MUST BE PURGED
        var expiredTempFile = Path.Combine(tempDir, $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(expiredTempFile, [70, 80]);
        File.SetLastWriteTimeUtc(expiredTempFile, now.AddHours(-48));

        // Case 5: Temporary file, age 10m -> MUST BE PRESERVED
        var recentTempFile = Path.Combine(tempDir, $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(recentTempFile, [90, 100]);
        File.SetLastWriteTimeUtc(recentTempFile, now.AddMinutes(-10));

        // Additional: Non-PNG file in permanent folder, age 48h -> MUST BE PRESERVED
        var nonPngFile = Path.Combine(permDir, "audit_log.txt");
        File.WriteAllText(nonPngFile, "some log");
        File.SetLastWriteTimeUtc(nonPngFile, now.AddHours(-48));

        // Additional: Malformed name in permanent folder, age 48h -> MUST BE PRESERVED (strict regex/format check)
        var malformedFile = Path.Combine(permDir, "invalid-key-format.png");
        File.WriteAllBytes(malformedFile, [1, 2]);
        File.SetLastWriteTimeUtc(malformedFile, now.AddHours(-48));

        // Act
        var deletedCount = await _worker.SweepOrphansOnceAsync(now, CancellationToken.None);

        // Assert: exactly Case 2 and Case 4 must be deleted (deletedCount == 2)
        Assert.Equal(2, deletedCount);

        // Case 1: committed permanent file preserved
        Assert.True(File.Exists(committedFile), "Committed permanent file with DB reference must be preserved.");

        // Case 2: crashed expired permanent orphan deleted
        Assert.False(File.Exists(crashedExpiredFile), "Permanent orphan exceeding grace period with no DB reference must be deleted.");

        // Case 3: recently promoted permanent file preserved by grace period
        Assert.True(File.Exists(crashedRecentFile), "Recently promoted permanent file must be preserved within grace period.");

        // Case 4: expired temp file deleted
        Assert.False(File.Exists(expiredTempFile), "Expired temporary file must be deleted.");

        // Case 5: recent temp file preserved
        Assert.True(File.Exists(recentTempFile), "Recent temporary file must be preserved.");

        // Additional cases preserved
        Assert.True(File.Exists(nonPngFile), "Non-png files must not be touched.");
        Assert.True(File.Exists(malformedFile), "Files with non-conforming key formats must not be touched.");
    }

    [Fact]
    public async Task SweepOrphansOnceAsync_WhenDatabaseQueryThrows_FailsClosedForPermanentAndCleansTemp()
    {
        var tenantId = Guid.NewGuid();
        var tempDir = Path.Combine(_testRoot, "tenants", tenantId.ToString("D"), "attempt-attachments-temp");
        var permDir = Path.Combine(_testRoot, "tenants", tenantId.ToString("D"), "attempt-attachments");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(permDir);

        var now = DateTime.UtcNow;

        // Permanent orphan file (48h old, unreferenced)
        var orphanNonce = Guid.NewGuid().ToString("N");
        var orphanPermFile = Path.Combine(permDir, $"{orphanNonce}.png");
        File.WriteAllBytes(orphanPermFile, [1, 2, 3]);
        File.SetLastWriteTimeUtc(orphanPermFile, now.AddHours(-48));

        // Expired temp file (48h old)
        var expiredTempFile = Path.Combine(tempDir, $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(expiredTempFile, [4, 5, 6]);
        File.SetLastWriteTimeUtc(expiredTempFile, now.AddHours(-48));

        // Mock a scope factory that throws an exception when resolving DbContext
        var throwingScopeMock = new Mock<IServiceScope>();
        var throwingSpMock = new Mock<IServiceProvider>();
        throwingSpMock.Setup(sp => sp.GetService(typeof(EduTwinDbContext)))
            .Throws(new InvalidOperationException("Simulated database connection failure / unavailable"));
        throwingScopeMock.SetupGet(s => s.ServiceProvider).Returns(throwingSpMock.Object);

        var throwingFactoryMock = new Mock<IServiceScopeFactory>();
        throwingFactoryMock.Setup(f => f.CreateScope()).Returns(throwingScopeMock.Object);

        var failClosedWorker = new AttachmentOrphanCleanupWorker(
            _options,
            _envMock.Object,
            throwingFactoryMock.Object,
            TimeProvider.System,
            NullLogger<AttachmentOrphanCleanupWorker>.Instance);

        // Act
        var deletedCount = await failClosedWorker.SweepOrphansOnceAsync(now, CancellationToken.None);

        // Assert: Fail-Closed invariant:
        // - Temp cleanup still executed -> expired temp file deleted (deletedCount == 1)
        // - Permanent cleanup skipped due to DB error -> permanent orphan PRESERVED!
        Assert.Equal(1, deletedCount);
        Assert.False(File.Exists(expiredTempFile), "Temporary file cleanup should still run independently.");
        Assert.True(File.Exists(orphanPermFile), "Permanent orphan must NOT be deleted when database query fails (Fail-Closed invariant).");
    }

    [Fact]
    public async Task SweepOrphansOnceAsync_NearExpiryPromotion_PreservesPermanentFileBeforeCommitAndPurgesAfterPromotionGrace()
    {
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var nonce = Guid.NewGuid().ToString("N");
        var tempDir = Path.Combine(_testRoot, "tenants", tenantId.ToString("D"), "attempt-attachments-temp");
        var permDir = Path.Combine(_testRoot, "tenants", tenantId.ToString("D"), "attempt-attachments");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(permDir);

        var t0 = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

        // 1. Temp file created at T0
        var tempFile = Path.Combine(tempDir, $"{nonce}.png");
        var dummyPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
        await File.WriteAllBytesAsync(tempFile, dummyPng);
        File.SetLastWriteTimeUtc(tempFile, t0);
        File.SetCreationTimeUtc(tempFile, t0);

        var sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(dummyPng)).ToLowerInvariant();

        // 2. Near token expiry: student submits at T0 + 23h 55m (token valid until T0 + 24h)
        var tPromote = t0.AddHours(23).AddMinutes(55);
        var mockTimeProvider = new Mock<TimeProvider>();
        mockTimeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(tPromote));

        var storage = new FileSystemAttemptAttachmentStorage(_options, _envMock.Object, mockTimeProvider.Object);
        var payload = new AttachmentUploadTokenPayload(
            tenantId,
            studentId,
            nonce,
            sha256,
            "drawing.png",
            dummyPng.Length,
            t0.AddHours(24));

        var promoted = await storage.PromoteToPermanentAsync(payload, CancellationToken.None);
        Assert.True(promoted.WasNewlyPromoted);

        var permFile = Path.Combine(permDir, $"{nonce}.png");
        Assert.True(File.Exists(permFile), "Permanent file must exist after promotion.");
        Assert.False(File.Exists(tempFile), "Temp file must be cleaned up after promotion.");

        // 3. Sweeper runs at T_sweep = T0 + 23h 56m (1 minute after promotion, BEFORE DB commit)
        var tSweepBeforeCommit = tPromote.AddMinutes(1);
        var deletedCountBeforeCommit = await _worker.SweepOrphansOnceAsync(tSweepBeforeCommit, CancellationToken.None);

        Assert.Equal(0, deletedCountBeforeCommit);
        Assert.True(File.Exists(permFile), "Permanent file MUST be preserved before DB commit because its promotion timestamp is recent (zero race condition).");

        // 4. Advance time past 24h grace period from promotion (simulating crash before DB commit)
        var tSweepAfterPromotionGrace = tPromote.AddHours(25);
        var deletedCountAfterCrash = await _worker.SweepOrphansOnceAsync(tSweepAfterPromotionGrace, CancellationToken.None);

        Assert.Equal(1, deletedCountAfterCrash);
        Assert.False(File.Exists(permFile), "Permanent file without DB reference MUST be purged once promotion grace period (24h) has elapsed.");
    }

    [Fact]
    public async Task SweepOrphansOnceAsync_AbandonedStagingFiles_PurgesExpiredStagingAndPreservesRecentStaging()
    {
        var tenantId = Guid.NewGuid();
        var permDir = Path.Combine(_testRoot, "tenants", tenantId.ToString("D"), "attempt-attachments");
        Directory.CreateDirectory(permDir);

        var now = DateTime.UtcNow;

        // Expired staging file (>24h old) -> MUST BE PURGED
        var expiredStagingFile = Path.Combine(permDir, $"{Guid.NewGuid():N}.staging.{Guid.NewGuid():N}.tmp");
        File.WriteAllBytes(expiredStagingFile, [1, 2, 3]);
        File.SetLastWriteTimeUtc(expiredStagingFile, now.AddHours(-30));

        // Recent staging file (<24h old, e.g. 5m old) -> MUST BE PRESERVED
        var recentStagingFile = Path.Combine(permDir, $"{Guid.NewGuid():N}.staging.{Guid.NewGuid():N}.tmp");
        File.WriteAllBytes(recentStagingFile, [4, 5, 6]);
        File.SetLastWriteTimeUtc(recentStagingFile, now.AddMinutes(-5));

        // Act
        var deletedCount = await _worker.SweepOrphansOnceAsync(now, CancellationToken.None);

        // Assert
        Assert.Equal(1, deletedCount);
        Assert.False(File.Exists(expiredStagingFile), "Expired staging file must be purged.");
        Assert.True(File.Exists(recentStagingFile), "Recent staging file must be preserved.");
    }
}
