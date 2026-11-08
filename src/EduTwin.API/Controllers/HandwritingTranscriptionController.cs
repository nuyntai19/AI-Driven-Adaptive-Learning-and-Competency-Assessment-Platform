using Microsoft.Extensions.Logging;
using System.Diagnostics;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

/// <summary>
/// Nhận dạng chữ viết tay từ ảnh nháp Scratchpad đã đính kèm trong trang làm bài.
/// Văn bản trả về chỉ điền vào ô lập luận để học sinh kiểm tra/sửa/xác nhận —
/// endpoint này không lưu và không chấm bất cứ thứ gì.
/// </summary>
[ApiController]
[Route("api/v1/learning")]
[Authorize(Policy = AuthorizationPolicies.StudentOnly)]
public sealed class HandwritingTranscriptionController(
    HandwritingTranscriptionService transcriptionService,
    TimeProvider timeProvider,
    ILogger<HandwritingTranscriptionController> logger) : ControllerBase
{
    public sealed record TranscribeHandwritingRequest(string? ImageBase64);

    public sealed record TranscribeHandwritingData(string Text);

    public sealed record TranscribeHandwritingResponse(TranscribeHandwritingData Data, MetaDto Meta);

    [HttpPost("handwriting/transcribe")]
    [Authorize(Policy = "learning.attempts.submit")]
    [ProducesResponseType(typeof(TranscribeHandwritingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Transcribe(
        [FromBody] TranscribeHandwritingRequest request,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (request is null || !HandwritingTranscriptionService.TryDecodePng(request.ImageBase64, out var png))
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/bad-request",
                "Dữ liệu không hợp lệ",
                "Ảnh chữ viết không hợp lệ. Hãy đính kèm lại ảnh nháp PNG (tối đa 5 MB).",
                traceId,
                "HANDWRITING_IMAGE_INVALID"));
        }

        try
        {
            var text = await transcriptionService.TranscribeAsync(png, cancellationToken);
            return Ok(new TranscribeHandwritingResponse(
                new TranscribeHandwritingData(text),
                new MetaDto { TraceId = traceId, Timestamp = timeProvider.GetUtcNow().UtcDateTime }));
        }
        catch (GeminiAdapterException ex) when (ex.ErrorCode == "AI_PROVIDER_CONFIGURATION_INVALID")
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblemDetails(
                    StatusCodes.Status503ServiceUnavailable,
                    "https://edutwin.local/problems/service-unavailable",
                    "Tính năng chưa được cấu hình",
                    "Nhận dạng chữ viết chưa được cấu hình AI (Gemini) trên máy chủ. Bạn vẫn có thể gõ trực tiếp vào ô lập luận.",
                    traceId,
                    "AI_PROVIDER_NOT_CONFIGURED"));
        }
        catch (GeminiAdapterException ex)
        {
            logger.LogWarning(ex, "Handwriting transcription failed with {ErrorCode}", ex.ErrorCode);
            return StatusCode(
                StatusCodes.Status502BadGateway,
                CreateProblemDetails(
                    StatusCodes.Status502BadGateway,
                    "https://edutwin.local/problems/bad-gateway",
                    "Không thể nhận dạng chữ viết",
                    "Hệ thống AI không phản hồi. Hãy thử lại, hoặc gõ trực tiếp vào ô bài làm.",
                    traceId,
                    ex.ErrorCode));
        }
    }

    private ProblemDetails CreateProblemDetails(
        int status,
        string type,
        string title,
        string detail,
        string traceId,
        string errorCode) =>
        new()
        {
            Type = type,
            Title = title,
            Status = status,
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = traceId,
                ["errorCode"] = errorCode
            }
        };
}
