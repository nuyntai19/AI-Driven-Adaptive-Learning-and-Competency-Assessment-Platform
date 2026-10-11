using System.Text.Json;
using EduTwin.API.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.Platform.Operations;
using EduTwin.Contracts.Platform;
using Microsoft.Extensions.Options;

namespace EduTwin.API.PlatformOperations;

public sealed class OperationalHostProbe(IOptions<AttachmentStorageOptions> storage, IOptions<PlatformOperationsOptions> options,
    IWebHostEnvironment environment, TimeProvider clock) : IOperationalHostProbe
{
    public (long? Total, long? Free) Volume()
    {
        try
        {
            var root = Path.GetFullPath(storage.Value.RootPath ?? Path.Combine(environment.ContentRootPath, "storage"));
            var drive = DriveInfo.GetDrives().Where(x => x.IsReady && root.StartsWith(x.Name, StringComparison.Ordinal))
                .OrderByDescending(x => x.Name.Length).FirstOrDefault();
            return drive is null ? (null, null) : (drive.TotalSize, drive.AvailableFreeSpace);
        }
        catch (IOException) { return (null, null); }
        catch (UnauthorizedAccessException) { return (null, null); }
    }

    public BackupObservation Backup()
    {
        var path = options.Value.BackupManifestPath;
        if (string.IsNullOrWhiteSpace(path)) return new();
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > 4096) return new();
            var data = JsonSerializer.Deserialize<BackupObservation>(File.ReadAllText(path));
            var now = clock.GetUtcNow().UtcDateTime;
            if (data is null || data.Status is not ("Success" or "Failed" or "Unknown") || data.Bytes < 0 ||
                new[] { data.LastAttemptAt, data.LastSuccessAt, data.LastRestoreVerifiedAt }.Any(d => d > now.AddMinutes(5)) ||
                data.Sha256 is { } sha && !System.Text.RegularExpressions.Regex.IsMatch(sha, "^[a-f0-9]{64}$") ||
                data.RestoreVerifiedSha256 is { } restoredSha && !System.Text.RegularExpressions.Regex.IsMatch(restoredSha, "^[a-f0-9]{64}$") ||
                data.LastRestoreStatus is not (null or "Success" or "Failed")) return new();
            return data;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
}
