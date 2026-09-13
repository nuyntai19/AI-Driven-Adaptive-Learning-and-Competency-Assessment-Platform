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
    [RequestSizeLimit(FileSystemAttemptAttachmentStorage.MaxFileBytes)]
    [ProducesResponseType(typeof(PrepareAttemptAttachmentUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> PrepareUpload(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (file is null)
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Dữ liệu không hợp lệ",
                "Cần gửi đúng một tệp PNG trong trường 'file'.",
                traceId,
                ErrorCodes.ValidationFailed));
        }

        if (file.Length > FileSystemAttemptAttachmentStorage.MaxFileBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, CreateProblem(
                StatusCodes.Status413PayloadTooLarge,
                "Tệp quá lớn",
                "Kích thước ảnh PNG không được vượt quá 5 MB.",
                traceId,
                ErrorCodes.ValidationFailed));
        }

        await using var content = file.OpenReadStream();
        var result = await _prepareUpload.ExecuteAsync(content, file.FileName, cancellationToken);
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

    [HttpGet("/api/v1/learning/attempts/{attemptId:ulong}/attachment")]
    [Authorize(Policy = CompositePermissionPolicies.AttemptsRead)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileStreamResult))]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        [FromRoute] ulong attemptId,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
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
                ErrorCodes.ResourceNotFound));
        }
    }

    private ProblemDetails CreateProblem(
        int status,
        string title,
        string detail,
        string traceId,
        string errorCode) => new()
        {
            Type = status == StatusCodes.Status404NotFound
                ? "https://edutwin.local/problems/not-found"
                : "https://edutwin.local/problems/validation",
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
