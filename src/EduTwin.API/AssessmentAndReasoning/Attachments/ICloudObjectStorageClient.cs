using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.API.AssessmentAndReasoning.Attachments;

public sealed record CloudStorageObjectInfo(string Name, DateTime? UpdatedAtUtc, DateTime? CreatedAtUtc);

public interface ICloudObjectStorageClient
{
    bool IsConfigured { get; }
    Task UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken cancellationToken);
    Task MoveAsync(string bucket, string sourcePath, string destinationPath, CancellationToken cancellationToken);
    Task<Stream?> DownloadAsync(string bucket, string path, CancellationToken cancellationToken);
    Task DeleteAsync(string bucket, string path, CancellationToken cancellationToken);
    Task<IReadOnlyList<CloudStorageObjectInfo>> ListObjectsAsync(string bucket, string prefix, int limit, int offset, CancellationToken cancellationToken);
}
