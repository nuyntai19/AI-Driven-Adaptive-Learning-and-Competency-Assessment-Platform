using System.Security.Cryptography;

namespace EduTwin.BLL.CurriculumAndQuestions.Import;

/// <summary>Private disk staging. Quotas include a conservative 100 MiB reservation while parsing.</summary>
public sealed class QuestionImportMediaStore : IDisposable
{
    private const long MiB = 1024 * 1024;
    private static readonly Lazy<QuestionImportMediaStore> Default = new(() => new());
    public static QuestionImportMediaStore Shared => Default.Value;
    private readonly object _gate = new();
    private readonly Dictionary<string, QuestionImportMediaAttempt> _attempts = new(StringComparer.Ordinal);
    private readonly string _runRoot;
    private readonly FileStream _runLease;
    private readonly Action<string> _deleteDirectory;
    private readonly Timer _cleanupTimer;
    private bool _disposed;
    private bool _leaseClosed;
    public QuestionImportMediaStore(string? stagingRoot = null, Action<string>? deleteDirectory = null)
    {
        var root = Path.GetFullPath(stagingRoot ?? Path.Combine(Path.GetTempPath(), "edutwin-question-import-v2"));
        CreatePrivateDirectory(root);
        _deleteDirectory = deleteDirectory ?? DeletePrivateTree;
        // The startup lock prevents another process observing a half-created run directory.
        using var startupLock = NewPrivateFile(Path.Combine(root, ".startup.lock"), FileMode.OpenOrCreate);
        foreach (var path in Directory.EnumerateDirectories(root, "run-*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(path);
            if (name.Length != 36 || !Guid.TryParseExact(name[4..], "N", out _) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
            try
            {
                ValidateOwnedPath(root, path);
                using (NewPrivateFile(Path.Combine(path, ".lease"), FileMode.OpenOrCreate)) { }
                DeletePrivateTree(path);
            }
            catch (IOException) { /* A live process retains its lease, or cleanup can retry on next startup. */ }
            catch (UnauthorizedAccessException) { }
        }
        _runRoot = Path.Combine(root, "run-" + Guid.NewGuid().ToString("N"));
        CreatePrivateDirectory(_runRoot);
        _runLease = NewPrivateFile(Path.Combine(_runRoot, ".lease"), FileMode.CreateNew);
        _cleanupTimer = new Timer(_ => RetryCleanup(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }
    public QuestionImportMediaAttempt CreateAttempt(Guid centerId, Guid actorId)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            RetryCleanupCore();
            const long reservation = 100 * MiB;
            if (_attempts.Values.Sum(a => a.ReservedBytes) + reservation > 1024 * MiB ||
                _attempts.Values.Where(a => a.CenterId == centerId).Sum(a => a.ReservedBytes) + reservation > 500 * MiB ||
                _attempts.Values.Where(a => a.CenterId == centerId && a.ActorId == actorId).Sum(a => a.ReservedBytes) + reservation > 100 * MiB)
                throw new QuestionImportMediaException("IMPORT_MEDIA_QUOTA", "Dung lượng tạm đã đạt giới hạn. Hủy phiên cũ hoặc chờ phiên hết hạn trước khi tải lại.");
            var id = Guid.NewGuid().ToString("N");
            var attempt = new QuestionImportMediaAttempt(this, id, centerId, actorId, Path.Combine(_runRoot, id), reservation);
            CreatePrivateDirectory(attempt.WorkDirectory); _attempts.Add(id, attempt); return attempt;
        }
    }
    internal void Seal(QuestionImportMediaAttempt attempt, HashSet<string> usedIds)
    {
        lock (_gate)
        {
            foreach (var asset in attempt.Assets.Values.Where(a => !usedIds.Contains(a.AssetId)).ToList())
            { ValidateOwnedPath(attempt.WorkDirectory, asset.Path); File.Delete(asset.Path); attempt.Assets.Remove(asset.Reference); }
            attempt.ReservedBytes = attempt.Assets.Values.Sum(a => a.Bytes);
        }
    }
    internal void Release(QuestionImportMediaAttempt attempt)
    {
        lock (_gate)
        {
            if (!_attempts.ContainsKey(attempt.Id) || !attempt.CanRelease) return;
            try
            {
                ValidateOwnedPath(_runRoot, attempt.WorkDirectory);
                _deleteDirectory(attempt.WorkDirectory);
                _attempts.Remove(attempt.Id);
                FinishDisposeIfEmpty();
            }
            catch (IOException) { /* Keep the reservation charged; periodic sweep retries. */ }
            catch (UnauthorizedAccessException) { }
        }
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return; _disposed = true;
            foreach (var attempt in _attempts.Values.ToList()) attempt.Dispose();
            FinishDisposeIfEmpty();
        }
    }
    public void RetryCleanup() { lock (_gate) RetryCleanupCore(); }
    private void RetryCleanupCore()
    {
        foreach (var attempt in _attempts.Values.Where(a => a.CanRelease).ToList()) Release(attempt);
        FinishDisposeIfEmpty();
    }
    private void FinishDisposeIfEmpty()
    {
        if (!_disposed || _attempts.Count != 0 || _leaseClosed) return;
        _leaseClosed = true; _cleanupTimer.Dispose(); _runLease.Dispose();
        try { DeletePrivateTree(_runRoot); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    internal static void CreatePrivateDirectory(string path)
    {
        EnsureNoReparseParents(path);
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
        else
        {
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        EnsureNoReparseParents(path);
    }
    internal static FileStream NewPrivateFile(string path, FileMode mode)
    {
        EnsureNoReparseParents(path);
        var options = new FileStreamOptions { Mode = mode, Access = FileAccess.ReadWrite, Share = FileShare.None, Options = FileOptions.Asynchronous | FileOptions.SequentialScan };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        var stream = new FileStream(path, options);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return stream;
    }
    internal static void ValidateOwnedPath(string root, string path)
    {
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new IOException("Private staging path is outside its owning directory.");
        EnsureNoReparseParents(path);
    }
    private static void EnsureNoReparseParents(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if (new FileInfo(current).LinkTarget is not null || ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0))
                throw new IOException("Private staging does not allow links or reparse points.");
    }
    private static void DeletePrivateTree(string path)
    {
        if (new DirectoryInfo(path).LinkTarget is not null) { Directory.Delete(path, false); return; }
        if (!Directory.Exists(path)) return;
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0) { Directory.Delete(path, false); return; }
        foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly))
        {
            var flags = File.GetAttributes(entry);
            if ((flags & FileAttributes.Directory) != 0) DeletePrivateTree(entry);
            else File.Delete(entry);
        }
        Directory.Delete(path, false);
    }
}

public sealed record StagedQuestionImportAsset(string Reference, string AssetId, string Path, string Sha256, long Bytes, int Width, int Height);
public sealed class QuestionImportMediaAttempt : IDisposable
{
    private readonly QuestionImportMediaStore _store;
    private readonly object _gate = new();
    private int _leases;
    private bool _disposeRequested;
    internal QuestionImportMediaAttempt(QuestionImportMediaStore store, string id, Guid centerId, Guid actorId, string path, long reserved)
    { _store = store; Id = id; CenterId = centerId; ActorId = actorId; WorkDirectory = path; ReservedBytes = reserved; }
    internal string Id { get; }
    public Guid CenterId { get; }
    public Guid ActorId { get; }
    public string WorkDirectory { get; }
    internal long ReservedBytes { get; set; }
    internal Dictionary<string, StagedQuestionImportAsset> Assets { get; } = new(StringComparer.Ordinal);
    internal bool CanRelease { get { lock (_gate) return _disposeRequested && _leases == 0; } }
    public async Task AddNormalizedAsync(string reference, NormalizedQuestionImportImage image, CancellationToken cancellationToken)
    {
        if (Assets.Count >= 20 || Assets.Values.Sum(a => a.Bytes) + image.Data.LongLength > 40L * 1024 * 1024)
            throw new QuestionImportMediaException("IMPORT_IMAGE_LIMIT", "Mỗi lô chỉ hỗ trợ tối đa 20 ảnh và 40 MiB ảnh đã chuẩn hóa.");
        var id = Guid.NewGuid().ToString("N"); var path = Path.Combine(WorkDirectory, id + ".png");
        QuestionImportMediaStore.ValidateOwnedPath(WorkDirectory, path);
        await using (var output = QuestionImportMediaStore.NewPrivateFile(path, FileMode.CreateNew)) await output.WriteAsync(image.Data, cancellationToken);
        Assets.Add(reference, new(reference, id, path, image.Sha256, image.Data.LongLength, image.Width, image.Height));
    }
    public void Seal(HashSet<string> usedIds) => _store.Seal(this, usedIds);
    public IDisposable? AcquireLease()
    {
        lock (_gate) { if (_disposeRequested) return null; _leases++; return new Lease(this); }
    }
    public async Task<byte[]> ReadVerifiedAsync(StagedQuestionImportAsset asset, CancellationToken cancellationToken)
    {
        QuestionImportMediaStore.ValidateOwnedPath(WorkDirectory, asset.Path);
        var info = new FileInfo(asset.Path);
        if (!info.Exists || info.Length != asset.Bytes || info.Length is 0 or > QuestionImportImageNormalizer.MaxOutputBytes)
            throw new QuestionImportMediaException("IMPORT_ASSET_CHANGED", "Ảnh tạm đã đổi hoặc không còn tồn tại. Hãy tải lại tệp.");
        await using var input = new FileStream(asset.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var bytes = new byte[checked((int)asset.Bytes)]; await input.ReadExactlyAsync(bytes, cancellationToken);
        if (await input.ReadAsync(new byte[1], cancellationToken) != 0 || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(asset.Sha256, StringComparison.Ordinal))
            throw new QuestionImportMediaException("IMPORT_ASSET_CHANGED", "Ảnh tạm không khớp mã kiểm tra. Hãy tải lại tệp.");
        return bytes;
    }
    public void Dispose()
    {
        lock (_gate) { _disposeRequested = true; if (_leases != 0) return; }
        _store.Release(this);
    }
    private sealed class Lease(QuestionImportMediaAttempt owner) : IDisposable
    {
        private QuestionImportMediaAttempt? _owner = owner;
        public void Dispose()
        {
            var value = Interlocked.Exchange(ref _owner, null); if (value is null) return;
            bool release; lock (value._gate) { value._leases--; release = value._disposeRequested && value._leases == 0; }
            if (release) value._store.Release(value);
        }
    }
}
