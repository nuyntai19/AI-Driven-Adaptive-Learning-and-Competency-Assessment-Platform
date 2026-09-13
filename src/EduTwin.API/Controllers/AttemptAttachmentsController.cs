using System.Diagnostics;
using EduTwin.API.AssessmentAndReasoning.Attachments;
using EduTwin.API.Security;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/learning/attempts/attachments")]
[Authorize]
public sealed class AttemptAttachmentsController : ControllerBase
{
    private readonly IPrepareAttemptAttachmentUploadUseCase _prepareUpload;
    private readonly IGetAttemptAttachmentUseCase _getAttachment;
    private readonly IAttemptAttachmentStorage _storage;
    private readonly TimeProvider _timeProvider;

    public AttemptAttachmentsController(
        IPrepareAttemptAttachmentUploadUseCase prepareUpload,
        IGetAttemptAttachmentUseCase getAttachment,
        IAttemptAttachmentStorage storage,
        TimeProvider timeProvider)
    {
        _prepareUpload = prepareUpload;
        _getAttachment = getAttachment;
        _storage = storage;
        _timeProvider = timeProvider;
    }

    [HttpPost("prepare-upload")]
    [Authorize(Policy = AuthorizationPolicies.StudentOnly)]
    [Authorize(Policy = "learning.attempts.submit")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(PrepareAttemptAttachmentUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> PrepareUpload(CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        if (string.IsNullOrWhiteSpace(Request.ContentType) ||
            !Request.ContentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Dữ liệu không hợp lệ",
                "Cần gửi request định dạng multipart/form-data.",
                traceId,
                ErrorCodes.ValidationFailed));
        }

        var boundary = HeaderUtilities.RemoveQuotes(
            MediaTypeHeaderValue.Parse(Request.ContentType).Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary))
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Dữ liệu không hợp lệ",
                "Thiếu multipart boundary.",
                traceId,
                ErrorCodes.ValidationFailed));
        }

        var reader = new MultipartReader(boundary, Request.Body)
        {
            HeadersCountLimit = 16,
            HeadersLengthLimit = 2048,
            BodyLengthLimit = 6 * 1024 * 1024
        };

        MultipartSection? section;
        string? targetFileName = null;
        using var stream = new MemoryStream();
        var foundFile = false;

        while ((section = await reader.ReadNextSectionAsync(cancellationToken)) != null)
        {
            var hasContentDisposition = ContentDispositionHeaderValue.TryParse(
                section.ContentDisposition, out var contentDisposition);

            if (hasContentDisposition && contentDisposition != null &&
                contentDisposition.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase) &&
                (contentDisposition.Name.Equals("file", StringComparison.OrdinalIgnoreCase) || !foundFile))
            {
                foundFile = true;
                targetFileName = contentDisposition.FileName.Value
                    ?? contentDisposition.FileNameStar.Value
                    ?? "drawing.png";

                var buffer = new byte[16 * 1024];
                int bytesRead;
                long totalBytes = 0;

                while ((bytesRead = await section.Body.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    totalBytes += bytesRead;
                    if (totalBytes > FileSystemAttemptAttachmentStorage.MaxFileBytes)
                    {
                        return StatusCode(StatusCodes.Status413PayloadTooLarge, CreateProblem(
                            StatusCodes.Status413PayloadTooLarge,
                            "Tệp quá lớn",
                            "Kích thước ảnh PNG không được vượt quá 5 MB.",
                            traceId,
                            ErrorCodes.ValidationFailed));
                    }

                    await stream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }

                break;
            }
        }

        if (!foundFile || stream.Length == 0)
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Dữ liệu không hợp lệ",
                "Cần gửi đúng một tệp PNG trong trường 'file'.",
                traceId,
                ErrorCodes.ValidationFailed));
        }

        stream.Position = 0;
        var result = await _prepareUpload.ExecuteAsync(stream, targetFileName ?? "drawing.png", cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(new PrepareAttemptAttachmentUploadResponse
            {
                Data = new PrepareAttemptAttachmentUploadDataDto
                {
                    DrawingUploadToken = result.DrawingUploadToken ?? throw new InvalidOperationException(
                        "Attachment upload preparation succeeded without an upload token."),
                    ExpiresAtUtc = result.ExpiresAtUtc ?? throw new InvalidOperationException(
                        "Attachment upload preparation succeeded without an expiry time.")
                },
                Meta = new MetaDto
                {
                    TraceId = traceId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        var status = result.ErrorCode == ErrorCodes.ResourceNotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;
        return StatusCode(status, CreateProblem(
            status,
            status == StatusCodes.Status404NotFound ? "Không tìm thấy dữ liệu" : "Dữ liệu không hợp lệ",
            status == StatusCodes.Status404NotFound
                ? "Không tìm thấy học sinh trong phạm vi hiện tại."
                : "Tệp đính kèm không hợp lệ.",
            traceId,
            result.ErrorCode ?? ErrorCodes.ValidationFailed));
    }

    [HttpGet("/api/v1/learning/attempts/{attemptId}/attachment")]
    [Authorize(Policy = CompositePermissionPolicies.AttemptsRead)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileStreamResult))]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        [FromRoute] ulong attemptId,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (attemptId == 0)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Không tìm thấy dữ liệu",
                "Không tìm thấy tệp đính kèm trong phạm vi truy cập hiện tại.",
                traceId,
                ErrorCodes.ResourceNotFound));
        }

        var result = await _getAttachment.ExecuteAsync(attemptId, cancellationToken);
        if (!result.IsSuccess || result.Attachment is null)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Không tìm thấy dữ liệu",
                "Không tìm thấy tệp đính kèm trong phạm vi truy cập hiện tại.",
                traceId,
                ErrorCodes.ResourceNotFound));
        }

        try
        {
            var content = await _storage.OpenPermanentReadAsync(result.Attachment.StorageKey, cancellationToken);
            return File(content, result.Attachment.ContentType, result.Attachment.FileName, enableRangeProcessing: false);
        }
        catch (FileNotFoundException)
        {
            // A missing blob is an infrastructure inconsistency, not permission evidence.
            return StatusCode(StatusCodes.Status503ServiceUnavailable, CreateProblem(
                StatusCodes.Status503ServiceUnavailable,
                "Tệp tạm thời không khả dụng",
                "Hệ thống đang khôi phục tệp đính kèm. Hãy thử lại sau.",
                traceId,
                ErrorCodes.StorageUnavailable));
        }
    }

    private ProblemDetails CreateProblem(
        int status,
        string title,
        string detail,
        string traceId,
        string errorCode) => new()
        {
            Type = status switch
            {
                StatusCodes.Status404NotFound => "https://edutwin.local/problems/not-found",
                StatusCodes.Status503ServiceUnavailable => "https://edutwin.local/problems/storage-unavailable",
                _ => "https://edutwin.local/problems/validation"
            },
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
