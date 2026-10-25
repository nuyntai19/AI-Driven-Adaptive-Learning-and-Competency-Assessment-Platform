using System.Diagnostics;
using EduTwin.BLL.CurriculumAndQuestions.Import;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/questions/import-v2")]
[Authorize]
public sealed class QuestionsImportV2Controller(IQuestionImportV2UseCase imports, TimeProvider clock) : ControllerBase
{
    [HttpPost("preview")]
    [Authorize(Policy = "curriculum.questions.create")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    public async Task<IActionResult> Preview([FromForm] IFormFile file, [FromForm] QuestionImportV2PreviewRequest request, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is 0 or > 30 * 1024 * 1024) return Failure("VALIDATION_FAILED", "Tệp phải lớn hơn 0 và không vượt 30 MiB.");
        await using var stream = file.OpenReadStream();
        var result = await imports.PreviewV2Async(stream, file.FileName, request, cancellationToken);
        return result.IsSuccess ? Ok(new QuestionImportPreviewResponse { Data = result.Data!, Meta = Meta() }) : Failure(result.ErrorCode!, result.ErrorMessage!);
    }
    [HttpPost("confirm")]
    [Authorize(Policy = "curriculum.questions.create")]
    public async Task<IActionResult> Confirm([FromBody] QuestionImportConfirmRequest request, CancellationToken cancellationToken)
    {
        var result = await imports.ConfirmV2Async(request, cancellationToken);
        return result.IsSuccess ? Ok(new QuestionImportConfirmResponse { Data = result.Data!, Meta = Meta() }) : Failure(result.ErrorCode!, result.ErrorMessage!);
    }
    [HttpGet("assets/{assetId}")]
    [Authorize(Policy = "curriculum.questions.create")]
    public async Task<IActionResult> Image(string assetId, [FromQuery] int revision, [FromHeader(Name = "X-Import-Preview-Token")] string? token, CancellationToken cancellationToken)
    {
        var result = await imports.GetPreviewImageAsync(token ?? string.Empty, revision, assetId, cancellationToken);
        if (result is null) return Failure("NOT_FOUND", "Ảnh tạm không tồn tại hoặc không thuộc phiên hiện tại.");
        Response.Headers.CacheControl = "no-store, private"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Data, "image/png");
    }
    [HttpDelete("session")]
    [Authorize(Policy = "curriculum.questions.create")]
    public async Task<IActionResult> Discard([FromQuery] int revision, [FromHeader(Name = "X-Import-Preview-Token")] string? token, CancellationToken cancellationToken) =>
        await imports.DiscardSessionAsync(token ?? string.Empty, revision, cancellationToken) ? NoContent() : Failure("NOT_FOUND", "Phiên không tồn tại hoặc đang được nhập.");
    private MetaDto Meta() => new() { TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, Timestamp = clock.GetUtcNow().UtcDateTime };
    private ObjectResult Failure(string code, string message)
    {
        var status = code == "FORBIDDEN_RESOURCE" ? 403 : code == "NOT_FOUND" ? 404 : code == "IMPORT_BUSY" ? 429 : 400;
        return StatusCode(status, new ProblemDetails { Status = status, Title = "Không thể xử lý import V2", Detail = message, Instance = Request.Path,
            Extensions = { ["errorCode"] = code, ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier } });
    }
}
