using EduTwin.Contracts.CurriculumAndQuestions;
using Microsoft.Extensions.Logging;

namespace EduTwin.BLL.CurriculumAndQuestions.Import;

public sealed partial class QuestionImportUseCase
{
    public async Task<QuestionImportPreviewResult> PreviewV2Async(Stream stream, string fileName, QuestionImportV2PreviewRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetActor(out var centerId, out var actorId)) return QuestionImportPreviewResult.Failure("FORBIDDEN_RESOURCE", "Chỉ giáo viên hợp lệ trong trung tâm được nhập câu hỏi.");
        var error = ValidateDefaults(request);
        if (error is not null) return QuestionImportPreviewResult.ValidationFailed(error);
        lock (CacheGate)
        {
            CleanupExpired();
            if (PreviewCache.Count >= 256 || PreviewCache.Values.Sum(e => e.CacheBytes) >= MaxPreviewCacheBytes || PreviewCache.Values.Count(e => e.CenterId == centerId && e.ActorId == actorId) >= 8)
                return QuestionImportPreviewResult.Failure("IMPORT_SESSION_LIMIT", "Đã đạt giới hạn phiên xem trước. Hủy phiên cũ hoặc chờ hết hạn.");
        }
        if (!PreviewSlots.Wait(0, cancellationToken)) return QuestionImportPreviewResult.Failure("IMPORT_BUSY", "Hệ thống đang kiểm tra hai tệp khác. Vui lòng thử lại sau ít giây.");
        QuestionImportMediaAttempt? media = null;
        var retained = false;
        try
        {
            if (!await OwnerExistsAsync(centerId, actorId, cancellationToken) || !await SubjectExistsAsync(centerId, request.SubjectId, cancellationToken))
                return QuestionImportPreviewResult.Failure("NOT_FOUND", "Giáo viên hoặc môn học không còn hoạt động trong trung tâm.");
            media = (_mediaStore ?? QuestionImportMediaStore.Shared).CreateAttempt(centerId, actorId);
            var parsed = await QuestionImportV2FileParser.ParseAsync(stream, fileName, request.SourceProfile, media.WorkDirectory, cancellationToken);
            var rows = parsed.Content.Rows;
            var assigned = rows.Where(r => r.Values.TryGetValue("ImageRef", out var reference) && !string.IsNullOrWhiteSpace(reference)).ToList();
            if (assigned.Count > 20) throw new QuestionImportMediaException("IMPORT_IMAGE_LIMIT", "Mỗi lô chỉ được gán ảnh cho tối đa 20 câu hỏi.");
            var references = assigned.Select(r => r.Values["ImageRef"]).ToHashSet(StringComparer.Ordinal);
            var sources = parsed.MediaSources.Where(p => references.Contains(p.Key)).Select(p => p.Value).ToList();
            foreach (var source in sources) ValidateSourcePath(media, source.SourcePath);
            var sourcePaths = sources.Select(s => Path.GetFullPath(s.SourcePath)).ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var path in Directory.EnumerateFiles(media.WorkDirectory, "*", SearchOption.AllDirectories))
                if (!sourcePaths.Contains(Path.GetFullPath(path))) File.Delete(path);
            foreach (var sourceGroup in sources.GroupBy(s => Path.GetFullPath(s.SourcePath), OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).OrderByDescending(g => new FileInfo(g.Key).Length))
            {
                var path = sourceGroup.Key;
                var groupReferences = sourceGroup.Select(s => s.Reference).ToHashSet(StringComparer.Ordinal);
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var bytes = await ReadSourceImageAsync(path, cancellationToken);
                    // Free the raw source before adding normalized PNG bytes to the private directory.
                    File.Delete(path);
                    var image = await _imageNormalizer.NormalizeAsync(bytes, cancellationToken);
                    foreach (var source in sourceGroup)
                    {
                        // Native dimensions are retained before PNG downscaling, avoiding
                        // a false stretch diagnosis from output-pixel rounding on thin images.
                        if (source.DisplayAspectRatio is { } displayed && Math.Abs(displayed / image.SourceAspectRatio - 1) > .01)
                        {
                            foreach (var row in assigned.Where(r => r.Values["ImageRef"] == source.Reference))
                                row.Diagnostics.Add(new("UNSUPPORTED_IMAGE_TRANSFORM", "Ảnh Word bị kéo giãn không đều. Hãy xuất ảnh đúng hình dạng rồi chèn lại.", row.SheetName, row.RowIndex, "ImageRef") { SourceLocation = row.SourceLocation });
                            continue;
                        }
                        await media.AddNormalizedAsync(source.Reference, image, cancellationToken);
                    }
                }
                catch (QuestionImportMediaException ex)
                {
                    File.Delete(path);
                    foreach (var row in assigned.Where(r => groupReferences.Contains(r.Values["ImageRef"])))
                        row.Diagnostics.Add(new(ex.Code, ex.Message, row.SheetName, row.RowIndex, "ImageRef"));
                }
            }
            if (assigned.Sum(r => media.Assets.GetValueOrDefault(r.Values["ImageRef"])?.Bytes ?? 0) > 40L * 1024 * 1024)
                throw new QuestionImportMediaException("IMPORT_IMAGE_LIMIT", "Tổng ảnh gán cho các câu hỏi vượt 40 MiB sau chuẩn hóa.");
            var result = await PreviewCoreAsync(Stream.Null, fileName, request, cancellationToken, parsed.Content, media, parsed.SourceProfile, parsed.SourceFormat);
            if (cancellationToken.IsCancellationRequested)
            {
                if (result.Data is not null) RemovePreview(result.Data.PreviewToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            retained = result.IsSuccess;
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (QuestionImportFileException ex) { return QuestionImportPreviewResult.Failure(ex.Code, ex.Message); }
        catch (QuestionImportMediaException ex) { return QuestionImportPreviewResult.Failure(ex.Code, ex.Message); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _logger?.LogWarning("V2 import private staging failed. ExceptionType={ExceptionType}", ex.GetType().Name);
            return QuestionImportPreviewResult.Failure("IMPORT_MEDIA_FAILED", "Không thể xử lý tệp hoặc vùng ảnh tạm. Vui lòng kiểm tra tệp rồi thử lại.");
        }
        finally { if (!retained) media?.Dispose(); PreviewSlots.Release(); }
    }

    public Task<QuestionImportConfirmResult> ConfirmV2Async(QuestionImportConfirmRequest request, CancellationToken cancellationToken) => ConfirmCoreAsync(request, "2.0", cancellationToken);

    public async Task<QuestionImportPreviewImageResult?> GetPreviewImageAsync(string previewToken, int revision, string assetId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetActor(out var centerId, out var actorId) || string.IsNullOrWhiteSpace(previewToken) || string.IsNullOrWhiteSpace(assetId) ||
            !PreviewCache.TryGetValue(previewToken, out var entry) || entry.TemplateVersion != "2.0" || entry.CenterId != centerId || entry.ActorId != actorId || entry.ActorRole != _tenantContext.Role || entry.Revision != revision || entry.Media is null) return null;
        if (entry.ExpiresAt <= _timeProvider.GetUtcNow()) { if (!entry.IsProcessing) RemovePreview(previewToken); return null; }
        using var lease = entry.Media.AcquireLease(); if (lease is null) return null;
        var asset = entry.Media.Assets.Values.SingleOrDefault(a => a.AssetId == assetId); if (asset is null) return null;
        try { return new(await entry.Media.ReadVerifiedAsync(asset, cancellationToken), asset.Sha256); }
        catch (QuestionImportMediaException) { return null; }
        catch (IOException) { return null; }
    }

    public Task<bool> DiscardSessionAsync(string previewToken, int revision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetActor(out var centerId, out var actorId) || string.IsNullOrWhiteSpace(previewToken) || !PreviewCache.TryGetValue(previewToken, out var entry) ||
            entry.TemplateVersion != "2.0" || entry.CenterId != centerId || entry.ActorId != actorId || entry.Revision != revision || !entry.TryBeginProcessing()) return Task.FromResult(false);
        entry.Complete(); RemovePreview(previewToken); return Task.FromResult(true);
    }

    private static void ValidateSourcePath(QuestionImportMediaAttempt attempt, string path)
    {
        var root = Path.GetFullPath(attempt.WorkDirectory) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(path);
        if (!resolved.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            (File.GetAttributes(resolved) & FileAttributes.ReparsePoint) != 0)
            throw new QuestionImportMediaException("UNSAFE_MEDIA_PATH", "Đường dẫn ảnh không thuộc vùng tạm riêng của phiên.");
    }
    private static async Task<byte[]> ReadSourceImageAsync(string path, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (input.Length is 0 or > QuestionImportImageNormalizer.MaxSourceBytes) throw new QuestionImportMediaException("IMAGE_SIZE_LIMIT", "Ảnh gốc phải lớn hơn 0 và không vượt 8 MiB.");
        var bytes = new byte[checked((int)input.Length)]; await input.ReadExactlyAsync(bytes, cancellationToken);
        if (await input.ReadAsync(new byte[1], cancellationToken) != 0) throw new QuestionImportMediaException("IMAGE_SIZE_LIMIT", "Ảnh gốc đã thay đổi trong lúc đọc.");
        return bytes;
    }
}
