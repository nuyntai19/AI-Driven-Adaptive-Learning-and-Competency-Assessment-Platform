using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.BLL.IdentityAndTenancy;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/subjects")]
[Authorize]
public class SubjectsController : ControllerBase
{
    private readonly IListSubjectsUseCase _listSubjectsUseCase;
    private readonly ICreateSubjectUseCase _createSubjectUseCase;
    private readonly TimeProvider _timeProvider;

    public SubjectsController(IListSubjectsUseCase listSubjectsUseCase, ICreateSubjectUseCase createSubjectUseCase, TimeProvider timeProvider)
    {
        _listSubjectsUseCase = listSubjectsUseCase;
        _createSubjectUseCase = createSubjectUseCase;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    [ProducesResponseType(typeof(SubjectListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListSubjects([FromQuery] SubjectListQuery query, CancellationToken cancellationToken)
    {
        var result = await _listSubjectsUseCase.ExecuteAsync(query, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new SubjectListResponse
            {
                Data = result.Data ?? new System.Collections.Generic.List<SubjectDto>(),
                Meta = new EduTwin.Contracts.IdentityAndTenancy.MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4",
                Title = "Không tìm thấy dữ liệu",
                Status = 404,
                Detail = "Dữ liệu không tồn tại hoặc bạn không có quyền truy cập.",
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

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectRequest request, CancellationToken cancellationToken)
    {
        var result = await _createSubjectUseCase.ExecuteAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new
            {
                Data = result.Data!,
                Meta = new EduTwin.Contracts.IdentityAndTenancy.MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return StatusCode(StatusCodes.Status201Created, response);
        }

        var problem = new ProblemDetails
        {
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                ["errorCode"] = result.ErrorCode
            }
        };

        if (result.ErrorCode == ErrorCodes.ValidationFailed)
        {
            problem.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1";
            problem.Title = "Dữ liệu không hợp lệ";
            problem.Status = 400;
            problem.Detail = "Thông tin môn học cung cấp không hợp lệ.";
            return BadRequest(problem);
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            problem.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4";
            problem.Title = "Không tìm thấy dữ liệu";
            problem.Status = 404;
            problem.Detail = "Dữ liệu không tồn tại hoặc bạn không có quyền truy cập.";
            return NotFound(problem);
        }

        if (result.ErrorCode == ErrorCodes.DuplicateResource)
        {
            problem.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8";
            problem.Title = "Dữ liệu đã tồn tại";
            problem.Status = 409;
            problem.Detail = "Môn học này đã tồn tại trong trung tâm.";
            return Conflict(problem);
        }

        throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }
}
