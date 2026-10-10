using System.Buffers.Binary;
using System.IO.Compression;

namespace EduTwin.BLL.CurriculumAndQuestions;

public static class QuestionImageContent
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const string ImageOnlyText = "Đọc đề bài trong ảnh đính kèm.";
    private const string Prefix = "data:image/png;base64,";

    // Browser normalizes JPG/PNG/WebP through canvas. The server independently
    // checks PNG chunks, CRCs, dimensions, decompression and trailing bytes.
    public static bool TryDecode(string? dataUrl, out byte[] bytes)
    {
        bytes = [];
        if (dataUrl is null || !dataUrl.StartsWith(Prefix, StringComparison.Ordinal) ||
            dataUrl.Length > Prefix.Length + (MaxBytes + 2) / 3 * 4) return false;
        try
        {
            var data = Convert.FromBase64String(dataUrl[Prefix.Length..]);
            if (data.Length is < 45 or > MaxBytes || !data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return false;
            var offset = 8; var header = false; var end = false; var idatEnded = false;
            uint width = 0, height = 0; var channels = 0;
            using var compressed = new MemoryStream();
            while (offset < data.Length)
            {
                if (data.Length - offset < 12) return false;
                var length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
                if (length > MaxBytes || length > data.Length - offset - 12) return false;
                var n = (int)length; var type = data.AsSpan(offset + 4, 4); var payload = data.AsSpan(offset + 8, n);
                if (Crc(data.AsSpan(offset + 4, n + 4)) != BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8 + n, 4))) return false;
                if (!header)
                {
                    if (!type.SequenceEqual("IHDR"u8) || n != 13) return false;
                    width = BinaryPrimitives.ReadUInt32BigEndian(payload[..4]); height = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4));
                    channels = payload[9] switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => 0 };
                    if (width is 0 or > 2048 || height is 0 or > 2048 || payload[8] != 8 || channels == 0 || payload[10] != 0 || payload[11] != 0 || payload[12] != 0) return false;
                    header = true;
                }
                else if (type.SequenceEqual("IDAT"u8))
                {
                    if (idatEnded) return false;
                    compressed.Write(payload);
                }
                else if (type.SequenceEqual("IEND"u8))
                {
                    if (n != 0 || offset + 12 != data.Length) return false;
                    end = true; break;
                }
                else
                {
                    // Unknown critical chunks (including a second IHDR) are invalid.
                    if ((type[0] & 32) == 0) return false;
                    if (compressed.Length > 0) idatEnded = true;
                }
                offset += n + 12;
            }
            if (!header || !end || compressed.Length == 0) return false;
            compressed.Position = 0;
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            var row = new byte[checked((int)width * channels)];
            for (var y = 0u; y < height; y++)
            {
                var filter = zlib.ReadByte(); if (filter is < 0 or > 4) return false;
                zlib.ReadExactly(row);
            }
            if (zlib.ReadByte() != -1) return false;
            bytes = data; return true;
        }
        catch (Exception ex) when (ex is FormatException or IOException or OverflowException or ArgumentException) { return false; }
    }

    private static uint Crc(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var b in data) { crc ^= b; for (var k = 0; k < 8; k++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0); }
        return ~crc;
    }
}
