using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly IListStudentsUseCase _listStudentsUseCase;
    private readonly IGetStudentUseCase _getStudentUseCase;
    private readonly ICreateStudentUseCase _createStudentUseCase;
    private readonly TimeProvider _timeProvider;

    public StudentsController(
        IListStudentsUseCase listStudentsUseCase,
        IGetStudentUseCase getStudentUseCase,
        ICreateStudentUseCase createStudentUseCase,
        TimeProvider timeProvider)
    {
        _listStudentsUseCase = listStudentsUseCase;
        _getStudentUseCase = getStudentUseCase;
        _createStudentUseCase = createStudentUseCase;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    public async Task<IActionResult> ListStudents([FromQuery] StudentListQuery query, CancellationToken cancellationToken)
    {
        var result = await _listStudentsUseCase.ExecuteAsync(query, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new StudentListResponse
            {
                Data = (System.Collections.Generic.IReadOnlyList<StudentDto>)result.Data!,
                Meta = new PagedMetaDto
                {
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalItems = result.TotalItems,
                    TotalPages = result.TotalPages,
                    TraceId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        if (result.ErrorCode == ErrorCodes.ValidationFailed)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu đầu vào không hợp lệ.",
                Detail = "Vui lòng kiểm tra lại thông tin cung cấp.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode }
            });
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
                Status = StatusCodes.Status404NotFound,
                Title = "Không tìm thấy dữ liệu.",
                Detail = "Không tìm thấy học viên hoặc dữ liệu không khả dụng.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode }
            });
        }

        if (result.ErrorCode == ErrorCodes.ForbiddenResource)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
                Status = StatusCodes.Status403Forbidden,
                Title = "Bạn không có quyền truy cập.",
                Detail = "Tài khoản của bạn không được phép thực hiện hành động này.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode }
            });
        }

        throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }

    [HttpGet("{studentId}")]
    [Authorize]
    public async Task<IActionResult> GetStudent(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _getStudentUseCase.ExecuteAsync(studentId, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new StudentDetailResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
                Status = StatusCodes.Status404NotFound,
                Title = "Không tìm thấy dữ liệu.",
                Detail = "Không tìm thấy học viên hoặc dữ liệu không khả dụng.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode }
            });
        }

        if (result.ErrorCode == ErrorCodes.ForbiddenResource)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
                Status = StatusCodes.Status403Forbidden,
                Title = "Bạn không có quyền truy cập.",
                Detail = "Tài khoản của bạn không được phép thực hiện hành động này.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode }
            });
        }

        throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentRequest request, CancellationToken cancellationToken)
    {
        var result = await _createStudentUseCase.ExecuteAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new StudentDetailResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return CreatedAtAction(nameof(GetStudent), new { studentId = result.Data!.StudentId }, response);
        }

        if (result.ErrorCode == ErrorCodes.ValidationFailed)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu đầu vào không hợp lệ.",
                Detail = "Vui lòng kiểm tra lại thông tin cung cấp.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode }
            });
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
                Status = StatusCodes.Status404NotFound,
                Title = "Không tìm thấy dữ liệu.",
                Detail = "Lớp học không tồn tại hoặc không hợp lệ.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode }
            });
        }

        if (result.ErrorCode == ErrorCodes.DuplicateResource)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
                Status = StatusCodes.Status409Conflict,
                Title = "Dữ liệu đã tồn tại.",
                Detail = "Tên đăng nhập này đã tồn tại trong trung tâm.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode }
            });
        }

        if (result.ErrorCode == ErrorCodes.ForbiddenResource)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
                Status = StatusCodes.Status403Forbidden,
                Title = "Bạn không có quyền truy cập.",
                Detail = "Tài khoản của bạn không được phép thực hiện hành động này.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode }
            });
        }

        throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }
}
