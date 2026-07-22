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
    private readonly IGetSubjectUseCase _getSubjectUseCase;
    private readonly IUpdateSubjectUseCase _updateSubjectUseCase;
    private readonly IDeleteSubjectUseCase _deleteSubjectUseCase;
    private readonly TimeProvider _timeProvider;

    public SubjectsController(IListSubjectsUseCase listSubjectsUseCase, ICreateSubjectUseCase createSubjectUseCase, IGetSubjectUseCase getSubjectUseCase, IUpdateSubjectUseCase updateSubjectUseCase, IDeleteSubjectUseCase deleteSubjectUseCase, TimeProvider timeProvider)
    {
        _listSubjectsUseCase = listSubjectsUseCase;
        _createSubjectUseCase = createSubjectUseCase;
        _getSubjectUseCase = getSubjectUseCase;
        _updateSubjectUseCase = updateSubjectUseCase;
        _deleteSubjectUseCase = deleteSubjectUseCase;
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

    [HttpGet("{subjectId:guid}")]
    [ProducesResponseType(typeof(SubjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubject(Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await _getSubjectUseCase.ExecuteAsync(subjectId, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new SubjectResponse
            {
                Data = result.Data!,
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

    [HttpPatch("{subjectId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(SubjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSubject(Guid subjectId, [FromBody] UpdateSubjectRequest request, CancellationToken cancellationToken)
    {
        var result = await _updateSubjectUseCase.ExecuteAsync(subjectId, request, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new SubjectResponse
            {
                Data = result.Data!,
                Meta = new EduTwin.Contracts.IdentityAndTenancy.MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        if (result.ErrorCode == ErrorCodes.ValidationFailed)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
                Title = "Dữ liệu không hợp lệ",
                Status = 400,
                Detail = "Dữ liệu đầu vào không đúng định dạng hoặc thiếu thông tin bắt buộc.",
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

        if (result.ErrorCode == ErrorCodes.DuplicateResource)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
                Title = "Dữ liệu đã tồn tại",
                Status = 409,
                Detail = "Môn học này đã tồn tại trong trung tâm.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.DuplicateResource
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.ConcurrencyConflict)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
                Title = "Xung đột dữ liệu",
                Status = 409,
                Detail = "Dữ liệu đã bị thay đổi bởi người dùng khác. Vui lòng làm mới trang và thử lại.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.ConcurrencyConflict
                }
            });
        }

        throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }

    [HttpDelete("{subjectId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CenterManagerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteSubject(Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await _deleteSubjectUseCase.ExecuteAsync(subjectId, cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
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

        if (result.ErrorCode == ErrorCodes.InvalidStateTransition)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
                Title = "Không thể xóa dữ liệu",
                Status = 409,
                Detail = "Không thể xóa môn học này do đang có dữ liệu liên kết (lớp học, giáo trình, mục tiêu học tập, hoặc dữ liệu phân tích).",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.InvalidStateTransition
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.ConcurrencyConflict)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
                Title = "Xung đột dữ liệu",
                Status = 409,
                Detail = "Dữ liệu đã bị thay đổi bởi người dùng khác. Vui lòng làm mới trang và thử lại.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.ConcurrencyConflict
                }
            });
        }

        throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }
}
