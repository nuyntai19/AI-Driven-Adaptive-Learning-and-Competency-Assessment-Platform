using Google.GenAI.Types;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.AI;

/// <summary>
/// Chép chữ viết tay của học sinh (ảnh PNG từ bảng nháp Scratchpad) thành văn bản
/// bằng chính Gemini vision adapter đang dùng để chấm ảnh nháp.
/// Kết quả chỉ là GỢI Ý: học sinh phải xem lại, sửa và xác nhận trong ô lập luận
/// trước khi nộp. Chưa cấu hình Gemini thì báo lỗi cấu hình rõ ràng —
/// tuyệt đối không giả lập kết quả chép.
/// </summary>
public sealed class HandwritingTranscriptionService(
    IGeminiGenerateContentClient client,
    IOptions<GeminiOptions> options)
{
    private readonly GeminiOptions _options = options.Value;

    public const int MaxImageBytes = 5 * 1024 * 1024;

    public const string Prompt =
        "Bạn là công cụ chép lại chữ viết tay, KHÔNG phải người chấm bài. " +
        "Hãy chép lại CHÍNH XÁC toàn bộ nội dung viết tay trong ảnh, theo đúng thứ tự từng dòng và từng bước giải. " +
        "Công thức toán học: ghi bằng LaTeX đặt trong $...$ khi đọc rõ; nếu không chắc, ghi dạng văn bản thuần gần nhất. " +
        "Không giải thích, không nhận xét, không tự sửa lỗi của học sinh, không thêm nội dung không có trong ảnh. " +
        "Phần nào thật sự không đọc được thì ghi [không rõ]. Chỉ trả về nội dung đã chép, không thêm lời mở đầu hay kết luận.";

    public async Task<string> TranscribeAsync(byte[] pngImage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pngImage);
        if (pngImage.Length == 0 || pngImage.Length > MaxImageBytes)
            throw GeminiAdapterException.RequestFailed();
        // Fail fast with the house configuration error instead of a provider
        // call that cannot succeed; never fake a transcription.
        if (_options.GetAllApiKeys().Count == 0 || string.IsNullOrWhiteSpace(_options.Model))
            throw GeminiAdapterException.ConfigurationInvalid();

        var result = await client.GenerateContentWithImagesAsync(
            _options.Model!, Prompt,
            [new GeminiInlineImagePart(pngImage, "image/png")],
            new GenerateContentConfig { CandidateCount = 1, Temperature = 0, MaxOutputTokens = 4096 },
            cancellationToken);
        var text = result.ResponseText?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw GeminiAdapterException.ResponseEmpty();
        return text;
    }

    /// <summary>Giải mã ảnh PNG gửi lên dạng base64 (chấp nhận tiền tố data URL) và kiểm tra chữ ký PNG.</summary>
    public static bool TryDecodePng(string? imageBase64, out byte[] png)
    {
        png = [];
        if (string.IsNullOrWhiteSpace(imageBase64)) return false;
        var base64 = imageBase64.Trim();
        if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = base64.IndexOf(',');
            if (comma < 0) return false;
            base64 = base64[(comma + 1)..];
        }
        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch (FormatException) { return false; }
        if (bytes.Length == 0 || bytes.Length > MaxImageBytes) return false;
        // PNG signature: 89 50 4E 47 0D 0A 1A 0A — the Gemini adapter only accepts image/png.
        if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
            return false;
        png = bytes;
        return true;
    }
}
