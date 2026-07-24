using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/curriculums")]
[Authorize]
public class CurriculumsController : ControllerBase
{
    private readonly ICreateCurriculumUseCase _createCurriculumUseCase;
    private readonly TimeProvider _timeProvider;

    public CurriculumsController(
        ICreateCurriculumUseCase createCurriculumUseCase,
        TimeProvider timeProvider)
    {
        _createCurriculumUseCase = createCurriculumUseCase;
        _timeProvider = timeProvider;
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(CurriculumResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCurriculum(
        [FromBody] CreateCurriculumRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createCurriculumUseCase.ExecuteAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new CurriculumResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Created(string.Empty, response);
        }

        if (result.ErrorCode == ErrorCodes.ValidationFailed)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
                Title = "Dữ liệu không hợp lệ",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Dữ liệu gửi lên không đúng định dạng hoặc thiếu thông tin.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.ValidationFailed
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4",
                Title = "Không tìm thấy dữ liệu",
                Status = StatusCodes.Status404NotFound,
                Detail = "Dữ liệu liên quan không tồn tại hoặc bạn không có quyền truy cập.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.ResourceNotFound
                }
            });
        }

        throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }
}
