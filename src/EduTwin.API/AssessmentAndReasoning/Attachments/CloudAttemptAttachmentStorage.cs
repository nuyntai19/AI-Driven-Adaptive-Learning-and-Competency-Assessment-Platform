using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.Attachments;

public sealed class CloudAttemptAttachmentStorage : IAttemptAttachmentStorage
{
    private readonly FileSystemAttemptAttachmentStorage _local;
    private readonly ICloudObjectStorageClient _cloud;
    private readonly AttachmentStorageOptions _options;
    private readonly ILogger<CloudAttemptAttachmentStorage> _logger;

    public CloudAttemptAttachmentStorage(
        FileSystemAttemptAttachmentStorage local,
        ICloudObjectStorageClient cloud,
        IOptions<AttachmentStorageOptions> options,
        ILogger<CloudAttemptAttachmentStorage> logger)
    {
        _local = local ?? throw new ArgumentNullException(nameof(local));
        _cloud = cloud ?? throw new ArgumentNullException(nameof(cloud));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StoredTemporaryAttachment> StoreTemporaryPngAsync(
        Guid centerId,
        string uploadNonce,
        Stream content,
        CancellationToken cancellationToken)
    {
        var stored = await _local.StoreTemporaryPngAsync(centerId, uploadNonce, content, cancellationToken);

        if (_cloud.IsConfigured)
        {
            try
            {
                var cloudPath = $"tenants/{centerId:D}/attempt-attachments-temp/{uploadNonce}.png";
                var tempPath = _local.ResolveSafePath(cloudPath);

                if (File.Exists(tempPath))
                {
                    await using var fileStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await _cloud.UploadAsync(_options.BucketName, cloudPath, fileStream, "image/png", cancellationToken);
                    _logger.LogInformation("Synced temporary attachment to cloud: {CloudPath}", cloudPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload temporary attachment to cloud. Local fallback remains active.");
            }
        }

        return stored;
    }

    public async Task<PromotedAttemptAttachment> PromoteToPermanentAsync(
        AttachmentUploadTokenPayload payload,
        CancellationToken cancellationToken)
    {
        var promoted = await _local.PromoteToPermanentAsync(payload, cancellationToken);

        if (_cloud.IsConfigured)
        {
            try
            {
                var tempCloudPath = $"tenants/{payload.CenterId:D}/attempt-attachments-temp/{payload.UploadNonce}.png";
                var permCloudPath = $"tenants/{payload.CenterId:D}/attempt-attachments/{payload.UploadNonce}.png";
                _ = _local.ResolveSafePath(tempCloudPath);
                _ = _local.ResolveSafePath(permCloudPath);

                await _cloud.MoveAsync(_options.BucketName, tempCloudPath, permCloudPath, cancellationToken);
                _logger.LogInformation("Promoted cloud attachment to permanent: {PermCloudPath}", permCloudPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to promote cloud attachment. Local copy preserved.");
            }
        }

        return promoted;
    }

    public async Task DeletePermanentAsync(string storageKey, CancellationToken cancellationToken)
    {
        // Validate path traversal before touching local or cloud storage
        _ = _local.ResolveSafePath(storageKey);

        await _local.DeletePermanentAsync(storageKey, cancellationToken);

        if (_cloud.IsConfigured)
        {
            try
            {
                await _cloud.DeleteAsync(_options.BucketName, NormalizeCloudKey(storageKey), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete cloud attachment {StorageKey}.", storageKey);
            }
        }
    }

    public async Task<Stream> OpenPermanentReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        // Validate path traversal upfront so invalid keys cannot escape root or query arbitrary cloud paths
        var localCachePath = _local.ResolveSafePath(storageKey);

        try
        {
            return await _local.OpenPermanentReadAsync(storageKey, cancellationToken);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            _logger.LogInformation("Local attachment cache miss for {StorageKey}. Fetching from Cloud Object Storage...", storageKey);
        }

        if (_cloud.IsConfigured)
        {
            var cloudStream = await _cloud.DownloadAsync(_options.BucketName, NormalizeCloudKey(storageKey), cancellationToken);
            if (cloudStream != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(localCachePath)!);
                    var tempCachePath = $"{localCachePath}.{Guid.NewGuid():N}.cache.tmp";
                    await using (var cacheFile = new FileStream(tempCachePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        await cloudStream.CopyToAsync(cacheFile, cancellationToken);
                    }
                    File.Move(tempCachePath, localCachePath, overwrite: true);
                    cloudStream.Position = 0;
                    _logger.LogInformation("Cached cloud attachment locally: {Path}", localCachePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to write local cache for {StorageKey}. Serving directly from cloud stream.", storageKey);
                    if (cloudStream.CanSeek)
                    {
                        cloudStream.Position = 0;
                    }
                }

                return cloudStream;
            }
        }

        throw new FileNotFoundException($"Attachment not found locally or in cloud storage: {storageKey}");
    }

    private static string NormalizeCloudKey(string storageKey) =>
        storageKey.Replace('\\', '/').Trim().TrimStart('/');
}
