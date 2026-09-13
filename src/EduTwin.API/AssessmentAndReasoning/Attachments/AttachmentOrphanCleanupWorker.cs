using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.DAL.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.Attachments;

/// <summary>
/// Sweeps expired temporary attempt attachments and unreferenced permanent attempt attachments across all tenants.
/// - Temporary attachments older than GracePeriodHours (default 24h) that were never promoted are deleted.
/// - Permanent attachments older than GracePeriodHours that have no database reference in AttemptAttachments
///   (e.g., caused by an unhandled crash or failure between permanent promotion and database transaction commit) are purged.
/// </summary>
public sealed class AttachmentOrphanCleanupWorker : BackgroundService
{
    private readonly string _rootPath;
    private readonly TimeSpan _gracePeriod;
    private readonly TimeSpan _tempGracePeriod;
    private readonly TimeSpan _interval;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AttachmentOrphanCleanupWorker> _logger;

    public AttachmentOrphanCleanupWorker(
        IOptions<AttachmentStorageOptions> options,
        IWebHostEnvironment environment,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<AttachmentOrphanCleanupWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        var configuredRoot = options.Value.RootPath;
        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(environment.ContentRootPath, "storage")
            : configuredRoot);
        _gracePeriod = TimeSpan.FromHours(Math.Max(AttachmentStorageOptions.MinimumGracePeriodHours, options.Value.GracePeriodHours));
        _tempGracePeriod = _gracePeriod.Add(TimeSpan.FromHours(AttachmentStorageOptions.TempSafetyMarginHours));
        _interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.CleanupIntervalMinutes));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AttachmentOrphanCleanupWorker started with interval {Interval}, permanent grace period {GracePeriod}, and temp grace period {TempGracePeriod}.",
            _interval,
            _gracePeriod,
            _tempGracePeriod);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
                var deletedCount = await SweepOrphansOnceAsync(utcNow, stoppingToken);
                if (deletedCount > 0)
                {
                    _logger.LogInformation("AttachmentOrphanCleanupWorker purged {Count} orphaned attachments.", deletedCount);
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
    /// Performs one cleanup sweep synchronously. Exposed for invocation in unit tests.
    /// </summary>
    public int SweepOrphansOnce(DateTime utcNow, CancellationToken cancellationToken = default) =>
        SweepOrphansOnceAsync(utcNow, cancellationToken).GetAwaiter().GetResult();

    /// <summary>
    /// Performs one asynchronous cleanup sweep across both temporary and permanent storage.
    /// </summary>
    public async Task<int> SweepOrphansOnceAsync(DateTime utcNow, CancellationToken cancellationToken = default)
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

        HashSet<string>? referencedKeys = null;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
            var rawKeys = await dbContext.AttemptAttachments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Select(a => a.StorageKey)
                .ToListAsync(cancellationToken);

            referencedKeys = rawKeys
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(NormalizeStorageKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query referenced attempt attachment keys from database. Permanent orphan cleanup will be skipped for this sweep (Fail-Closed).");
            referencedKeys = null;
        }

        var deletedCount = 0;
        var tenantDirs = Directory.GetDirectories(tenantsRoot);
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        foreach (var tenantDir in tenantDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tenantDirInfo = new DirectoryInfo(tenantDir);
            if (tenantDirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint) || tenantDirInfo.LinkTarget is not null)
            {
                _logger.LogWarning("Skipping reparse point or symlink directory: {TenantDir}", tenantDir);
                continue;
            }

            // 1. Temporary attachments sweep (*.png in attempt-attachments-temp older than temp grace period)
            deletedCount += SweepDirectoryFiles(
                tenantDir,
                "attempt-attachments-temp",
                rootWithSeparator,
                searchPattern: "*.png",
                shouldDelete: fileInfo => utcNow - fileInfo.LastWriteTimeUtc > _tempGracePeriod,
                logPrefix: "temporary",
                cancellationToken);

            // 2. Abandoned staging files sweep (*.staging.*.tmp in attempt-attachments older than grace period)
            deletedCount += SweepDirectoryFiles(
                tenantDir,
                "attempt-attachments",
                rootWithSeparator,
                searchPattern: "*.tmp",
                shouldDelete: fileInfo =>
                {
                    if (utcNow - fileInfo.LastWriteTimeUtc <= _gracePeriod)
                    {
                        return false;
                    }

                    return fileInfo.Name.Contains(".staging.", StringComparison.OrdinalIgnoreCase);
                },
                logPrefix: "abandoned staging",
                cancellationToken);

            // 3. Permanent attachments sweep (*.png in attempt-attachments older than grace period with no DB reference)
            // Fail-closed invariant: only sweep permanent files when referencedKeys was successfully queried from DB.
            if (referencedKeys is not null)
            {
                deletedCount += SweepDirectoryFiles(
                    tenantDir,
                    "attempt-attachments",
                    rootWithSeparator,
                    searchPattern: "*.png",
                    shouldDelete: fileInfo =>
                    {
                        if (utcNow - fileInfo.LastWriteTimeUtc <= _gracePeriod)
                        {
                            return false;
                        }

                        var relativeKey = NormalizeStorageKey(Path.GetRelativePath(_rootPath, fileInfo.FullName));
                        if (!IsValidPermanentStorageKey(relativeKey))
                        {
                            return false;
                        }

                        return !referencedKeys.Contains(relativeKey);
                    },
                    logPrefix: "unreferenced permanent",
                    cancellationToken);
            }
        }

        return deletedCount;
    }

    private int SweepDirectoryFiles(
        string tenantDir,
        string subFolderName,
        string rootWithSeparator,
        string searchPattern,
        Func<FileInfo, bool> shouldDelete,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        var targetDir = Path.Combine(tenantDir, subFolderName);
        if (!Directory.Exists(targetDir))
        {
            return 0;
        }

        var dirInfo = new DirectoryInfo(targetDir);
        if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint) || dirInfo.LinkTarget is not null)
        {
            _logger.LogWarning("Skipping reparse point or symlink directory: {TargetDir}", targetDir);
            return 0;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(targetDir, searchPattern);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Unable to enumerate attachments in {TargetDir}.", targetDir);
            return 0;
        }

        var deleted = 0;
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

                if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint) || fileInfo.LinkTarget is not null)
                {
                    _logger.LogWarning("Skipping reparse point or symlink file: {FilePath}", file);
                    continue;
                }

                var canonicalPath = Path.GetFullPath(file);
                if (!canonicalPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("File path {FilePath} escaped storage root {RootPath}", file, _rootPath);
                    continue;
                }

                if (shouldDelete(fileInfo))
                {
                    fileInfo.Delete();
                    deleted++;
                    _logger.LogInformation("Deleted {LogPrefix} attachment: {FilePath}", logPrefix, file);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to delete {LogPrefix} attachment {FilePath}.", logPrefix, file);
            }
        }

        return deleted;
    }

    private static string NormalizeStorageKey(string key) =>
        key.Replace('\\', '/').Trim().TrimStart('/');

    private static bool IsValidPermanentStorageKey(string relativeKey)
    {
        // Must strictly conform to tenants/{centerId}/attempt-attachments/{nonce}.png
        var parts = relativeKey.Split('/');
        if (parts.Length != 4)
        {
            return false;
        }

        if (!string.Equals(parts[0], "tenants", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Guid.TryParse(parts[1], out var centerId) || centerId == Guid.Empty)
        {
            return false;
        }

        if (!string.Equals(parts[2], "attempt-attachments", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = parts[3];
        if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var nonce = fileName[..^4];
        return Guid.TryParseExact(nonce, "N", out _) || Guid.TryParse(nonce, out _);
    }
}
