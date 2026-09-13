using System.Buffers.Binary;
using System.Security.Cryptography;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.Attachments;

/// <summary>
/// Local, tenant-isolated attachment store. It streams uploads to a temporary file,
/// validates PNG structure in full, and only promotes with no-overwrite semantics.
/// </summary>
public sealed class FileSystemAttemptAttachmentStorage : IAttemptAttachmentStorage
{
    public const long MaxFileBytes = 5_242_880;
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private readonly string _rootPath;

    public FileSystemAttemptAttachmentStorage(
        IOptions<AttachmentStorageOptions> options,
        IWebHostEnvironment environment)
    {
        var configuredRoot = options.Value.RootPath;
        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(environment.ContentRootPath, "storage")
            : configuredRoot);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredTemporaryAttachment> StoreTemporaryPngAsync(
        Guid centerId,
        string uploadNonce,
        Stream content,
        CancellationToken cancellationToken)
    {
        EnsureNonce(uploadNonce);
        var path = GetTenantPath(centerId, "attempt-attachments-temp", $"{uploadNonce}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        long totalBytes = 0;

        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var destination = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[64 * 1024];
                while (true)
                {
                    var read = await content.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (read == 0) break;
                    totalBytes += read;
                    if (totalBytes > MaxFileBytes)
                    {
                        throw new AttemptAttachmentValidationException("PNG attachment exceeds 5 MB.");
                    }
                    hash.AppendData(buffer, 0, read);
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            if (totalBytes == 0) throw new AttemptAttachmentValidationException("PNG attachment is empty.");
            await ValidatePngAsync(path, totalBytes, cancellationToken);
            return new StoredTemporaryAttachment(Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), totalBytes);
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    public async Task<PromotedAttemptAttachment> PromoteToPermanentAsync(
        AttachmentUploadTokenPayload payload,
        CancellationToken cancellationToken)
    {
        EnsureNonce(payload.UploadNonce);
        var temporaryPath = GetTenantPath(payload.CenterId, "attempt-attachments-temp", $"{payload.UploadNonce}.png");
        var storageKey = $"tenants/{payload.CenterId:D}/attempt-attachments/{payload.UploadNonce}.png";
        var permanentPath = GetSafePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(permanentPath)!);

        if (File.Exists(permanentPath))
        {
            await EnsureMatchingHashAsync(permanentPath, payload.Sha256Hex, cancellationToken);
            return new PromotedAttemptAttachment(storageKey, false);
        }

        await EnsureMatchingHashAsync(temporaryPath, payload.Sha256Hex, cancellationToken);
        var wasNewlyPromoted = false;
        try
        {
            File.Move(temporaryPath, permanentPath, overwrite: false);
            wasNewlyPromoted = true;
        }
        catch (IOException) when (File.Exists(permanentPath))
        {
            await EnsureMatchingHashAsync(permanentPath, payload.Sha256Hex, cancellationToken);
        }

        return new PromotedAttemptAttachment(storageKey, wasNewlyPromoted);
    }

    public Task DeletePermanentAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryDelete(GetSafePath(storageKey));
        return Task.CompletedTask;
    }

    public Task<Stream> OpenPermanentReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            GetSafePath(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    private async Task ValidatePngAsync(string path, long fileSize, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var signature = new byte[PngSignature.Length];
        await ReadExactlyAsync(stream, signature, cancellationToken);
        if (!signature.AsSpan().SequenceEqual(PngSignature)) throw new AttemptAttachmentValidationException("File is not a PNG.");

        var isFirstChunk = true;
        var foundEnd = false;
        while (stream.Position < fileSize)
        {
            var header = new byte[8];
            await ReadExactlyAsync(stream, header, cancellationToken);
            var length = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
            if (length > MaxFileBytes || length > fileSize - stream.Position - 4)
            {
                throw new AttemptAttachmentValidationException("PNG chunk length is invalid.");
            }

            var type = header.AsSpan(4, 4).ToArray();
            if (isFirstChunk)
            {
                if (!type.AsSpan().SequenceEqual("IHDR"u8) || length != 13)
                {
                    throw new AttemptAttachmentValidationException("PNG must begin with a valid IHDR chunk.");
                }
                var ihdr = new byte[13];
                await ReadExactlyAsync(stream, ihdr, cancellationToken);
                ValidateIhdr(ihdr);
                var declaredCrc = await ReadUInt32Async(stream, cancellationToken);
                if (CalculateCrc(type, ihdr) != declaredCrc) throw new AttemptAttachmentValidationException("PNG IHDR CRC is invalid.");
                isFirstChunk = false;
                continue;
            }

            var crc = new PngCrc(type);
            var remaining = checked((int)length);
            var buffer = new byte[Math.Min(64 * 1024, Math.Max(1, remaining))];
            while (remaining > 0)
            {
                var count = Math.Min(buffer.Length, remaining);
                await ReadExactlyAsync(stream, buffer.AsMemory(0, count), cancellationToken);
                crc.Append(buffer.AsSpan(0, count));
                remaining -= count;
            }
            var expectedCrc = await ReadUInt32Async(stream, cancellationToken);
            if (crc.GetHash() != expectedCrc) throw new AttemptAttachmentValidationException("PNG chunk CRC is invalid.");
            if (type.AsSpan().SequenceEqual("IEND"u8))
            {
                if (length != 0 || stream.Position != fileSize) throw new AttemptAttachmentValidationException("PNG has invalid trailing data.");
                foundEnd = true;
                break;
            }
        }

        if (isFirstChunk || !foundEnd) throw new AttemptAttachmentValidationException("PNG is truncated.");
    }

    private static void ValidateIhdr(ReadOnlySpan<byte> ihdr)
    {
        var width = BinaryPrimitives.ReadUInt32BigEndian(ihdr[..4]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(ihdr.Slice(4, 4));
        var bitDepth = ihdr[8];
        var colorType = ihdr[9];
        if (width is 0 or > 4096 || height is 0 or > 4096 ||
            bitDepth is not (8 or 16) || colorType is not (0 or 2 or 4 or 6) ||
            ihdr[10] != 0 || ihdr[11] != 0 || ihdr[12] is > 1)
        {
            throw new AttemptAttachmentValidationException("PNG IHDR values are not supported.");
        }
    }

    private async Task EnsureMatchingHashAsync(string path, string expectedHash, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualHash = await SHA256.HashDataAsync(stream, cancellationToken);
        var expectedBytes = Convert.FromHexString(expectedHash);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedBytes))
        {
            throw new InvalidOperationException("Attachment content does not match its protected upload token.");
        }
    }

    private string GetTenantPath(Guid centerId, string folder, string fileName) =>
        GetSafePath($"tenants/{centerId:D}/{folder}/{fileName}");

    private string GetSafePath(string relativePath)
    {
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Attachment storage path escaped the configured root.");
        }
        return path;
    }

    private static void EnsureNonce(string uploadNonce)
    {
        if (!Guid.TryParseExact(uploadNonce, "N", out _)) throw new InvalidOperationException("Attachment upload nonce is invalid.");
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (read == 0) throw new AttemptAttachmentValidationException("PNG is truncated.");
            offset += read;
        }
    }

    private static Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken) =>
        ReadExactlyAsync(stream, buffer.AsMemory(), cancellationToken);

    private static async Task<uint> ReadUInt32Async(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new byte[4];
        await ReadExactlyAsync(stream, bytes, cancellationToken);
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    private static uint CalculateCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = new PngCrc(type);
        crc.Append(data);
        return crc.GetHash();
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private struct PngCrc
    {
        private uint _value;
        public PngCrc(ReadOnlySpan<byte> initial)
        {
            _value = 0xFFFFFFFF;
            Append(initial);
        }
        public void Append(ReadOnlySpan<byte> data)
        {
            foreach (var value in data)
            {
                _value ^= value;
                for (var bit = 0; bit < 8; bit++) _value = (_value & 1) != 0 ? 0xEDB88320u ^ (_value >> 1) : _value >> 1;
            }
        }
        public uint GetHash() => ~_value;
    }
}
