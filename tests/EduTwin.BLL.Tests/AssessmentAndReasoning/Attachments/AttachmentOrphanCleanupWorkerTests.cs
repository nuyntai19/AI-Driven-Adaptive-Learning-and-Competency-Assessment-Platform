using System;
using System.IO;
using System.Threading;
using EduTwin.API.AssessmentAndReasoning.Attachments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Attachments;

public sealed class AttachmentOrphanCleanupWorkerTests : IDisposable
{
    private readonly string _testRoot;
    private readonly AttachmentOrphanCleanupWorker _worker;

    public AttachmentOrphanCleanupWorkerTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "edutwin-test-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        var options = Options.Create(new AttachmentStorageOptions
        {
            RootPath = _testRoot,
            GracePeriodHours = 24,
            CleanupIntervalMinutes = 60,
        });

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.SetupGet(e => e.ContentRootPath).Returns(_testRoot);

        _worker = new AttachmentOrphanCleanupWorker(
            options,
            envMock.Object,
            TimeProvider.System,
            NullLogger<AttachmentOrphanCleanupWorker>.Instance);
    }

    public void Dispose()
    {
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
    public void SweepOrphansOnce_DeletesExpiredTempPng_AndPreservesRecentAndPermanentFiles()
    {
        var tenantId = Guid.NewGuid();
        var tempDir = Path.Combine(_testRoot, "tenants", tenantId.ToString("D"), "attempt-attachments-temp");
        var permDir = Path.Combine(_testRoot, "tenants", tenantId.ToString("D"), "attempt-attachments");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(permDir);

        var now = DateTime.UtcNow;

        // 1. Expired temp png (>24h old) -> should be deleted
        var expiredTempFile = Path.Combine(tempDir, "expired.png");
        File.WriteAllBytes(expiredTempFile, [1, 2, 3]);
        File.SetLastWriteTimeUtc(expiredTempFile, now.AddHours(-26));

        // 2. Fresh temp png (<24h old) -> should be kept
        var recentTempFile = Path.Combine(tempDir, "recent.png");
        File.WriteAllBytes(recentTempFile, [4, 5, 6]);
        File.SetLastWriteTimeUtc(recentTempFile, now.AddHours(-2));

        // 3. Non-PNG file in temp folder -> should be ignored/kept
        var textFile = Path.Combine(tempDir, "notes.txt");
        File.WriteAllText(textFile, "keep me");
        File.SetLastWriteTimeUtc(textFile, now.AddHours(-30));

        // 4. File in permanent folder -> should never be touched by temp sweep
        var permFile = Path.Combine(permDir, "permanent.png");
        File.WriteAllBytes(permFile, [7, 8, 9]);
        File.SetLastWriteTimeUtc(permFile, now.AddHours(-48));

        var deletedCount = _worker.SweepOrphansOnce(now, CancellationToken.None);

        Assert.Equal(1, deletedCount);
        Assert.False(File.Exists(expiredTempFile), "Expired temporary attachment must be deleted.");
        Assert.True(File.Exists(recentTempFile), "Recent temporary attachment must be preserved.");
        Assert.True(File.Exists(textFile), "Non-png files in temp folder should not be deleted by worker.");
        Assert.True(File.Exists(permFile), "Permanent attachment must never be deleted by temp orphan worker.");
    }

    [Fact]
    public void SweepOrphansOnce_WhenNoTenantsExist_ReturnsZeroWithoutError()
    {
        var deletedCount = _worker.SweepOrphansOnce(DateTime.UtcNow, CancellationToken.None);
        Assert.Equal(0, deletedCount);
    }
}
