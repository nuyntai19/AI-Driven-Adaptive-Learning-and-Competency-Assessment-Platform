using System.Security.Cryptography;
using System.Buffers.Binary;
using SkiaSharp;

namespace EduTwin.BLL.CurriculumAndQuestions.Import;

/// <summary>One bounded native decoder for the process. Never returns source metadata or image bytes in JSON.</summary>
public sealed class QuestionImportImageNormalizer
{
    private static readonly SemaphoreSlim DecodeSlot = new(1, 1);
    public const int MaxSourceBytes = 8 * 1024 * 1024;
    public const int MaxOutputBytes = 2 * 1024 * 1024;
    public async Task<NormalizedQuestionImportImage> NormalizeAsync(byte[] source, CancellationToken cancellationToken)
    {
        if (source.Length is 0 or > MaxSourceBytes) throw new QuestionImportMediaException("IMAGE_SIZE_LIMIT", "Ảnh gốc phải lớn hơn 0 và không vượt 8 MiB.");
        RejectAnimationMarkers(source);
        await DecodeSlot.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var data = SKData.CreateCopy(source);
            using var codec = SKCodec.Create(data);
            if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp))
                throw new QuestionImportMediaException("UNSUPPORTED_IMAGE", "Chỉ hỗ trợ ảnh PNG, JPEG hoặc WebP hợp lệ.");
            var width = codec.Info.Width; var height = codec.Info.Height;
            if (width <= 0 || height <= 0 || (long)width * height > 24_000_000)
                throw new QuestionImportMediaException("IMAGE_PIXEL_LIMIT", "Ảnh không được vượt 24 megapixel.");
            if (codec.FrameCount > 1) throw new QuestionImportMediaException("ANIMATED_IMAGE", "Chỉ hỗ trợ ảnh tĩnh; không nhập ảnh động.");
            // Native decode is bounded by bytes/pixels and a single slot. Cancellation
            // is observed between native calls, not advertised as interrupting SKCodec.
            using var decoded = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
                throw new QuestionImportMediaException("INVALID_IMAGE", "Ảnh hỏng hoặc thiếu dữ liệu; không dùng bản giải mã không đầy đủ.");
            var origin = codec.EncodedOrigin;
            var rotated = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            var orientedWidth = rotated ? height : width; var orientedHeight = rotated ? width : height;
            var longest = Math.Min(2048, Math.Max(orientedWidth, orientedHeight));
            using var decodedImage = SKImage.FromBitmap(decoded);
            for (var attempt = 0; attempt < 12; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scale = longest / (double)Math.Max(orientedWidth, orientedHeight);
                var outputWidth = Math.Max(1, (int)Math.Round(orientedWidth * scale));
                var outputHeight = Math.Max(1, (int)Math.Round(orientedHeight * scale));
                using var bitmap = new SKBitmap(new SKImageInfo(outputWidth, outputHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.White);
                    canvas.Scale(outputWidth / (float)orientedWidth, outputHeight / (float)orientedHeight);
                    switch (origin)
                    {
                        case SKEncodedOrigin.TopRight: canvas.Translate(width, 0); canvas.Scale(-1, 1); break;
                        case SKEncodedOrigin.BottomRight: canvas.Translate(width, height); canvas.RotateDegrees(180); break;
                        case SKEncodedOrigin.BottomLeft: canvas.Translate(0, height); canvas.Scale(1, -1); break;
                        case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
                        case SKEncodedOrigin.RightTop: canvas.Translate(height, 0); canvas.RotateDegrees(90); break;
                        case SKEncodedOrigin.RightBottom: canvas.Translate(height, width); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
                        case SKEncodedOrigin.LeftBottom: canvas.Translate(0, width); canvas.RotateDegrees(-90); break;
                    }
                    using var paint = new SKPaint { IsAntialias = true };
                    canvas.DrawImage(decodedImage, new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear), paint);
                    canvas.Flush();
                }
                using var image = SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                if (encoded is not null && encoded.Size <= MaxOutputBytes)
                {
                    var bytes = encoded.ToArray();
                    if (!QuestionImageContent.TryValidate(bytes)) throw new QuestionImportMediaException("INVALID_NORMALIZED_IMAGE", "Ảnh chuẩn hóa không vượt qua bộ kiểm tra PNG của hệ thống.");
                    return new(bytes, outputWidth, outputHeight, Convert.ToHexString(SHA256.HashData(bytes))) { SourceAspectRatio = orientedWidth / (double)orientedHeight };
                }
                longest = Math.Max(1, (int)(longest * .8));
            }
            throw new QuestionImportMediaException("IMAGE_OUTPUT_LIMIT", "Không thể chuẩn hóa ảnh trong giới hạn PNG 2 MiB.");
        }
        finally { DecodeSlot.Release(); }
    }
    private static void RejectAnimationMarkers(ReadOnlySpan<byte> source)
    {
        var png = source.Length >= 8 && source[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var webp = source.Length >= 12 && source[..4].SequenceEqual("RIFF"u8) && source.Slice(8, 4).SequenceEqual("WEBP"u8);
        if (!png && !webp) return;
        for (var offset = png ? 8 : 12; offset <= source.Length - (png ? 12 : 8);)
        {
            var length = png ? BinaryPrimitives.ReadUInt32BigEndian(source.Slice(offset, 4)) : BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset + 4, 4));
            var overhead = png ? 12 : 8;
            if (length > source.Length - offset - overhead) throw new QuestionImportMediaException("INVALID_IMAGE", "Cấu trúc ảnh không hợp lệ.");
            var type = source.Slice(offset + (png ? 4 : 0), 4);
            if ((png && (type.SequenceEqual("acTL"u8) || type.SequenceEqual("fcTL"u8) || type.SequenceEqual("fdAT"u8))) ||
                (webp && (type.SequenceEqual("ANIM"u8) || type.SequenceEqual("ANMF"u8) || (type.SequenceEqual("VP8X"u8) && length > 0 && (source[offset + 8] & 2) != 0))))
                throw new QuestionImportMediaException("ANIMATED_IMAGE", "Chỉ hỗ trợ ảnh tĩnh; không nhận APNG hoặc WebP động.");
            offset += checked((int)length + overhead + (webp ? (int)(length & 1) : 0));
        }
    }
}

public sealed record NormalizedQuestionImportImage(byte[] Data, int Width, int Height, string Sha256)
{
    public double SourceAspectRatio { get; init; }
}
public sealed class QuestionImportMediaException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
