using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.Attachments;

/// <summary>
/// Sweeps expired temporary attempt attachments across all tenants.
/// Temporary attachments older than GracePeriodHours (default 24h) that were never
/// promoted via a submitted attempt are considered orphaned and deleted.
/// </summary>
public sealed class AttachmentOrphanCleanupWorker : BackgroundService
{
    private readonly string _rootPath;
    private readonly TimeSpan _gracePeriod;
    private readonly TimeSpan _interval;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AttachmentOrphanCleanupWorker> _logger;

    public AttachmentOrphanCleanupWorker(
        IOptions<AttachmentStorageOptions> options,
        IWebHostEnvironment environment,
        TimeProvider timeProvider,
        ILogger<AttachmentOrphanCleanupWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        var configuredRoot = options.Value.RootPath;
        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(environment.ContentRootPath, "storage")
            : configuredRoot);
        _gracePeriod = TimeSpan.FromHours(Math.Max(1, options.Value.GracePeriodHours));
        _interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.CleanupIntervalMinutes));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AttachmentOrphanCleanupWorker started with interval {Interval} and grace period {GracePeriod}.",
            _interval,
            _gracePeriod);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
                var deletedCount = SweepOrphansOnce(utcNow, stoppingToken);
                if (deletedCount > 0)
                {
                    _logger.LogInformation("AttachmentOrphanCleanupWorker purged {Count} orphaned temp attachments.", deletedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during orphan attachment cleanup sweep.");
            }

            try
            {
                await Task.Delay(_interval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("AttachmentOrphanCleanupWorker stopped.");
    }

    /// <summary>
    /// Performs one cleanup sweep. Exposed for direct invocation in unit tests.
    /// </summary>
    public int SweepOrphansOnce(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_rootPath))
        {
            return 0;
        }

        var tenantsRoot = Path.Combine(_rootPath, "tenants");
        if (!Directory.Exists(tenantsRoot))
        {
            return 0;
        }

        var deletedCount = 0;
        var tenantDirs = Directory.GetDirectories(tenantsRoot);

        foreach (var tenantDir in tenantDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tempDir = Path.Combine(tenantDir, "attempt-attachments-temp");
            if (!Directory.Exists(tempDir))
            {
                continue;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(tempDir, "*.png");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Unable to enumerate temp attachments in {TempDir}.", tempDir);
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var fileInfo = new FileInfo(file);
                    if (!fileInfo.Exists)
                    {
                        continue;
                    }

                    if (utcNow - fileInfo.LastWriteTimeUtc > _gracePeriod)
                    {
                        fileInfo.Delete();
                        deletedCount++;
                        _logger.LogDebug("Deleted orphaned temporary attachment {FilePath}", file);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Failed to delete orphaned temporary attachment {FilePath}.", file);
                }
            }
        }

        return deletedCount;
    }
}
