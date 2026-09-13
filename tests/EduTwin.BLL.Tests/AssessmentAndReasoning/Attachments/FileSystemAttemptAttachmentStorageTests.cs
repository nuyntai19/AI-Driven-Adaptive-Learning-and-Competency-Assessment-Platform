using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.API.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Attachments;

public sealed class FileSystemAttemptAttachmentStorageTests : IDisposable
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private readonly string _testRoot;
    private readonly FileSystemAttemptAttachmentStorage _storage;

    public FileSystemAttemptAttachmentStorageTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "edutwin-storage-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        var options = Options.Create(new AttachmentStorageOptions
        {
            RootPath = _testRoot,
            GracePeriodHours = 24,
            CleanupIntervalMinutes = 60,
        });

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.SetupGet(e => e.ContentRootPath).Returns(_testRoot);

        _storage = new FileSystemAttemptAttachmentStorage(options, envMock.Object);
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
    public async Task StoreAndPromote_ValidPng_SucceedsWithMatchingHashAndLifecycle()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var nonce = Guid.NewGuid().ToString("N");

        // 1x1 RGBA PNG: 1 filter byte (0) + 4 RGBA bytes = 5 bytes uncompressed
        var validPngBytes = BuildSyntheticPng(1, 1, 6, 8, [0, 255, 0, 0, 255]);
        var expectedSha256 = Convert.ToHexString(SHA256.HashData(validPngBytes)).ToLowerInvariant();

        // 1. Store temporary PNG
        using var stream = new MemoryStream(validPngBytes);
        var stored = await _storage.StoreTemporaryPngAsync(centerId, nonce, stream, CancellationToken.None);

        Assert.Equal(expectedSha256, stored.Sha256Hex);
        Assert.Equal(validPngBytes.Length, stored.FileSizeBytes);

        var tempPath = Path.Combine(_testRoot, "tenants", centerId.ToString("D"), "attempt-attachments-temp", $"{nonce}.png");
        Assert.True(File.Exists(tempPath), "Temporary PNG must exist on disk.");

        // 2. Promote to permanent
        var payload = new AttachmentUploadTokenPayload(
            centerId,
            studentId,
            nonce,
            expectedSha256,
            "drawing.png",
            validPngBytes.Length,
            DateTime.UtcNow.AddHours(24));

        var promoted = await _storage.PromoteToPermanentAsync(payload, CancellationToken.None);

        Assert.True(promoted.WasNewlyPromoted);
        var permPath = Path.Combine(_testRoot, "tenants", centerId.ToString("D"), "attempt-attachments", $"{nonce}.png");
        Assert.True(File.Exists(permPath), "Permanent PNG must exist on disk after promotion.");
        Assert.False(File.Exists(tempPath), "Temporary PNG must be moved upon promotion.");

        // 3. Idempotent promotion replay
        var replay = await _storage.PromoteToPermanentAsync(payload, CancellationToken.None);
        Assert.False(replay.WasNewlyPromoted, "Subsequent promotion of existing file must report WasNewlyPromoted = false.");
        Assert.Equal(promoted.StorageKey, replay.StorageKey);

        // 4. Open read stream and verify contents
        await using (var readStream = await _storage.OpenPermanentReadAsync(promoted.StorageKey, CancellationToken.None))
        using (var ms = new MemoryStream())
        {
            await readStream.CopyToAsync(ms);
            Assert.Equal(validPngBytes, ms.ToArray());
        }

        // 5. Delete permanent
        await _storage.DeletePermanentAsync(promoted.StorageKey, CancellationToken.None);
        Assert.False(File.Exists(permPath), "Permanent PNG must be deleted.");
    }

    [Fact]
    public async Task StoreTemporaryPngAsync_InvalidMagicBytes_ThrowsValidationException()
    {
        var centerId = Guid.NewGuid();
        var nonce = Guid.NewGuid().ToString("N");
        var badBytes = "NOT A PNG FILE"u8.ToArray();

        using var stream = new MemoryStream(badBytes);
        var ex = await Assert.ThrowsAsync<AttemptAttachmentValidationException>(() =>
            _storage.StoreTemporaryPngAsync(centerId, nonce, stream, CancellationToken.None));

        Assert.Contains("File is not a PNG", ex.Message);
    }

    [Fact]
    public async Task StoreTemporaryPngAsync_DimensionsExceed4096_ThrowsValidationException()
    {
        var centerId = Guid.NewGuid();
        var nonce = Guid.NewGuid().ToString("N");
        // 5000 x 100 dimensions
        var oversizedPng = BuildSyntheticPng(5000, 100, 6, 8, [0, 255, 0, 0, 255]);

        using var stream = new MemoryStream(oversizedPng);
        var ex = await Assert.ThrowsAsync<AttemptAttachmentValidationException>(() =>
            _storage.StoreTemporaryPngAsync(centerId, nonce, stream, CancellationToken.None));

        Assert.Contains("4096", ex.Message);
    }

    [Fact]
    public async Task StoreTemporaryPngAsync_UnsupportedColorType_ThrowsValidationException()
    {
        var centerId = Guid.NewGuid();
        var nonce = Guid.NewGuid().ToString("N");
        // Color type 5 is invalid in PNG standard
        var badColorPng = BuildSyntheticPng(1, 1, 5, 8, [0, 255, 0, 0, 255]);

        using var stream = new MemoryStream(badColorPng);
        var ex = await Assert.ThrowsAsync<AttemptAttachmentValidationException>(() =>
            _storage.StoreTemporaryPngAsync(centerId, nonce, stream, CancellationToken.None));

        Assert.Contains("not supported", ex.Message);
    }

    [Fact]
    public async Task StoreTemporaryPngAsync_MissingIdatChunk_ThrowsValidationException()
    {
        var centerId = Guid.NewGuid();
        var nonce = Guid.NewGuid().ToString("N");
        var noIdatPng = BuildSyntheticPng(1, 1, 6, 8, [0, 255, 0, 0, 255], omitIdat: true);

        using var stream = new MemoryStream(noIdatPng);
        var ex = await Assert.ThrowsAsync<AttemptAttachmentValidationException>(() =>
            _storage.StoreTemporaryPngAsync(centerId, nonce, stream, CancellationToken.None));

        Assert.Contains("missing IDAT chunk", ex.Message);
    }

    [Fact]
    public async Task StoreTemporaryPngAsync_CorruptedIdatStream_ThrowsValidationException()
    {
        var centerId = Guid.NewGuid();
        var nonce = Guid.NewGuid().ToString("N");
        var corruptIdatPng = BuildSyntheticPng(1, 1, 6, 8, [0, 255, 0, 0, 255], corruptIdat: true);

        using var stream = new MemoryStream(corruptIdatPng);
        var ex = await Assert.ThrowsAsync<AttemptAttachmentValidationException>(() =>
            _storage.StoreTemporaryPngAsync(centerId, nonce, stream, CancellationToken.None));

        Assert.Contains("zlib decompression failed", ex.Message);
    }

    [Fact]
    public async Task StoreTemporaryPngAsync_InvalidCrc_ThrowsValidationException()
    {
        var centerId = Guid.NewGuid();
        var nonce = Guid.NewGuid().ToString("N");
        var badCrcPng = BuildSyntheticPng(1, 1, 6, 8, [0, 255, 0, 0, 255], corruptCrc: true);

        using var stream = new MemoryStream(badCrcPng);
        var ex = await Assert.ThrowsAsync<AttemptAttachmentValidationException>(() =>
            _storage.StoreTemporaryPngAsync(centerId, nonce, stream, CancellationToken.None));

        Assert.Contains("CRC is invalid", ex.Message);
    }

    [Fact]
    public async Task StoreTemporaryPngAsync_DecompressionBomb_ExceedingMemoryBudget_ThrowsValidationException()
    {
        var centerId = Guid.NewGuid();
        var nonce = Guid.NewGuid().ToString("N");

        // 1x1 image claims to be tiny (expected raw bytes = 5, budget = 1 MB min)
        // But IDAT contains 2 MB of compressed zeros which decompress to 2 MB
        var hugeDecompressed = new byte[2 * 1024 * 1024];
        var bombPng = BuildSyntheticPng(1, 1, 6, 8, hugeDecompressed);

        using var stream = new MemoryStream(bombPng);
        var ex = await Assert.ThrowsAsync<AttemptAttachmentValidationException>(() =>
            _storage.StoreTemporaryPngAsync(centerId, nonce, stream, CancellationToken.None));

        Assert.Contains("memory budget", ex.Message);
    }

    [Fact]
    public async Task PromoteToPermanentAsync_HashMismatchWithToken_ThrowsInvalidOperationException()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var nonce = Guid.NewGuid().ToString("N");

        var validPngBytes = BuildSyntheticPng(1, 1, 6, 8, [0, 255, 0, 0, 255]);
        using var stream = new MemoryStream(validPngBytes);
        await _storage.StoreTemporaryPngAsync(centerId, nonce, stream, CancellationToken.None);

        // Token has a different SHA256 hash
        var fakeSha256 = new string('0', 64);
        var payload = new AttachmentUploadTokenPayload(
            centerId,
            studentId,
            nonce,
            fakeSha256,
            "drawing.png",
            validPngBytes.Length,
            DateTime.UtcNow.AddHours(24));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _storage.PromoteToPermanentAsync(payload, CancellationToken.None));
    }

    private static byte[] BuildSyntheticPng(
        uint width,
        uint height,
        byte colorType,
        byte bitDepth,
        byte[] uncompressedData,
        bool corruptCrc = false,
        bool omitIdat = false,
        bool corruptIdat = false)
    {
        using var ms = new MemoryStream();
        ms.Write(PngSignature);

        // 1. IHDR
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = bitDepth;
        ihdr[9] = colorType;
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace
        WriteChunk(ms, "IHDR"u8, ihdr, corruptCrc);

        // 2. IDAT (unless omitted)
        if (!omitIdat)
        {
            byte[] idatData;
            if (corruptIdat)
            {
                idatData = [0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA];
            }
            else
            {
                using var compMs = new MemoryStream();
                using (var zlib = new ZLibStream(compMs, CompressionLevel.Optimal, leaveOpen: true))
                {
                    zlib.Write(uncompressedData);
                }
                idatData = compMs.ToArray();
            }

            WriteChunk(ms, "IDAT"u8, idatData, corruptCrc);
        }

        // 3. IEND
        WriteChunk(ms, "IEND"u8, [], corruptCrc);

        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data, bool corruptCrc = false)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, (uint)data.Length);
        stream.Write(lengthBytes);
        stream.Write(type);
        stream.Write(data);

        var crc = ComputeCrc(type, data);
        if (corruptCrc) crc ^= 0xFFFFFFFFu;

        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static uint ComputeCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        void Append(ReadOnlySpan<byte> span)
        {
            foreach (var b in span)
            {
                crc ^= b;
                for (var i = 0; i < 8; i++)
                {
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
                }
            }
        }
        Append(type);
        Append(data);
        return ~crc;
    }
}
