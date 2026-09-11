using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.Dashboards;
using EduTwin.API.Security;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.Dashboards;
using EduTwin.Contracts.DigitalTwin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly IListStudentsUseCase _listStudentsUseCase;
    private readonly IGetStudentUseCase _getStudentUseCase;
    private readonly ICreateStudentUseCase _createStudentUseCase;
    private readonly IUpdateStudentUseCase _updateStudentUseCase;
    private readonly IUpsertStudentSubjectGoalUseCase _upsertStudentSubjectGoalUseCase;
    private readonly IListStudentSubjectGoalsUseCase _listStudentSubjectGoalsUseCase;
    private readonly IGetStudentDashboardUseCase _getStudentDashboardUseCase;
    private readonly IGetStudentTwinUseCase _getStudentTwinUseCase;
    private readonly IGetStudentTwinHistoryUseCase _getStudentTwinHistoryUseCase;
    private readonly TimeProvider _timeProvider;

    public StudentsController(
        IListStudentsUseCase listStudentsUseCase,
        IGetStudentUseCase getStudentUseCase,
        ICreateStudentUseCase createStudentUseCase,
        IUpdateStudentUseCase updateStudentUseCase,
        IUpsertStudentSubjectGoalUseCase upsertStudentSubjectGoalUseCase,
        IListStudentSubjectGoalsUseCase listStudentSubjectGoalsUseCase,
        TimeProvider timeProvider)
        : this(
            listStudentsUseCase,
            getStudentUseCase,
            createStudentUseCase,
            updateStudentUseCase,
            upsertStudentSubjectGoalUseCase,
            listStudentSubjectGoalsUseCase,
            null!,
            null!,
            null!,
            timeProvider)
    {
    }

    [ActivatorUtilitiesConstructor]
    public StudentsController(
        IListStudentsUseCase listStudentsUseCase,
        IGetStudentUseCase getStudentUseCase,
        ICreateStudentUseCase createStudentUseCase,
        IUpdateStudentUseCase updateStudentUseCase,
        IUpsertStudentSubjectGoalUseCase upsertStudentSubjectGoalUseCase,
        IListStudentSubjectGoalsUseCase listStudentSubjectGoalsUseCase,
        IGetStudentDashboardUseCase getStudentDashboardUseCase,
        IGetStudentTwinUseCase getStudentTwinUseCase,
        IGetStudentTwinHistoryUseCase getStudentTwinHistoryUseCase,
        TimeProvider timeProvider)
    {
        _listStudentsUseCase = listStudentsUseCase;
        _getStudentUseCase = getStudentUseCase;
        _createStudentUseCase = createStudentUseCase;
        _updateStudentUseCase = updateStudentUseCase;
        _upsertStudentSubjectGoalUseCase = upsertStudentSubjectGoalUseCase;
        _listStudentSubjectGoalsUseCase = listStudentSubjectGoalsUseCase;
        _getStudentDashboardUseCase = getStudentDashboardUseCase;
        _getStudentTwinUseCase = getStudentTwinUseCase;
        _getStudentTwinHistoryUseCase = getStudentTwinHistoryUseCase;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    [Authorize(Policy = "organization.students.read")]
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
    [Authorize(Policy = "organization.students.read")]
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
    [Authorize(Policy = "organization.students.create")]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentRequest request, CancellationToken cancellationToken)
    {
        var result = await _createStudentUseCase.ExecuteAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new StudentResponse
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

    [HttpPatch("{studentId}")]
    [Authorize(Policy = "organization.students.update")]
    public async Task<IActionResult> UpdateStudent(Guid studentId, [FromBody] UpdateStudentRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu đầu vào không hợp lệ.",
                Detail = "Vui lòng kiểm tra lại thông tin cung cấp.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = ErrorCodes.ValidationFailed }
            });
        }

        var result = await _updateStudentUseCase.ExecuteAsync(studentId, request, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new StudentResponse
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

        if (result.ErrorCode == ErrorCodes.ConcurrencyConflict)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
                Status = StatusCodes.Status409Conflict,
                Title = "Dữ liệu đã bị thay đổi.",
                Detail = "Thông tin học viên đã được cập nhật bởi một người khác. Vui lòng tải lại và thử lại.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode }
            });
        }

        throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }

    [HttpPut("{studentId}/goals/{subjectId}")]
    [Authorize(Policy = CompositePermissionPolicies.StudentTwinUpdate)]
    public async Task<IActionResult> UpsertStudentSubjectGoal(Guid studentId, Guid subjectId, [FromBody] UpsertStudentSubjectGoalRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu đầu vào không hợp lệ.",
                Detail = "Vui lòng kiểm tra lại thông tin cung cấp.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = ErrorCodes.ValidationFailed }
            });
        }

        var result = await _upsertStudentSubjectGoalUseCase.ExecuteAsync(studentId, subjectId, request, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new StudentSubjectGoalResponse
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

        if (result.ErrorCode == ErrorCodes.ConcurrencyConflict)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
                Status = StatusCodes.Status409Conflict,
                Title = "Dữ liệu đã bị thay đổi.",
                Detail = "Thông tin đã được cập nhật bởi một người khác. Vui lòng thử lại.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode }
            });
        }

        throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }

    [HttpGet("{studentId}/goals")]
    [Authorize(Policy = CompositePermissionPolicies.StudentTwinRead)]
    public async Task<IActionResult> ListStudentSubjectGoals(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _listStudentSubjectGoalsUseCase.ExecuteAsync(studentId, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new StudentSubjectGoalListResponse
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

    [HttpGet("me/dashboard")]
    [Authorize(Policy = "dashboards.student.read_own")]
    public async Task<IActionResult> GetStudentDashboard(
        [FromQuery] Guid subjectId,
        CancellationToken cancellationToken)
    {
        var traceId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (subjectId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu không hợp lệ.",
                Detail = "Mã môn học (subjectId) không hợp lệ.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = ErrorCodes.ValidationFailed }
            });
        }

        var result = await _getStudentDashboardUseCase.ExecuteAsync(subjectId, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(new StudentDashboardResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = traceId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
                Status = StatusCodes.Status404NotFound,
                Title = "Không tìm thấy dữ liệu.",
                Detail = result.ErrorMessage ?? "Không tìm thấy thông tin bảng điều khiển.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = ErrorCodes.ResourceNotFound }
            });
        }

        if (result.ErrorCode == ErrorCodes.ForbiddenResource)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
                Status = StatusCodes.Status403Forbidden,
                Title = "Không có quyền truy cập.",
                Detail = result.ErrorMessage ?? "Bạn không có quyền truy cập bảng điều khiển này.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = ErrorCodes.ForbiddenResource }
            });
        }

        return BadRequest(new ProblemDetails
        {
            Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Dữ liệu không hợp lệ.",
            Detail = result.ErrorMessage ?? "Yêu cầu không hợp lệ.",
            Instance = HttpContext.Request.Path,
            Extensions = { ["traceId"] = traceId, ["errorCode"] = result.ErrorCode ?? ErrorCodes.ValidationFailed }
        });
    }

    [HttpGet("me/twin")]
    [Authorize(Policy = "twin.student.read_own")]
    public async Task<IActionResult> GetStudentTwin(
        [FromQuery] Guid subjectId,
        CancellationToken cancellationToken)
    {
        var traceId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (subjectId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu không hợp lệ.",
                Detail = "Mã môn học (subjectId) không hợp lệ.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = ErrorCodes.ValidationFailed }
            });
        }

        var result = await _getStudentTwinUseCase.ExecuteAsync(subjectId, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(new StudentTwinResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = traceId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
                Status = StatusCodes.Status404NotFound,
                Title = "Không tìm thấy dữ liệu.",
                Detail = result.ErrorMessage ?? "Không tìm thấy thông tin Digital Twin.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = ErrorCodes.ResourceNotFound }
            });
        }

        if (result.ErrorCode == ErrorCodes.ForbiddenResource)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
                Status = StatusCodes.Status403Forbidden,
                Title = "Không có quyền truy cập.",
                Detail = result.ErrorMessage ?? "Bạn không có quyền truy cập Digital Twin này.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = ErrorCodes.ForbiddenResource }
            });
        }

        return BadRequest(new ProblemDetails
        {
            Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Dữ liệu không hợp lệ.",
            Detail = result.ErrorMessage ?? "Yêu cầu không hợp lệ.",
            Instance = HttpContext.Request.Path,
            Extensions = { ["traceId"] = traceId, ["errorCode"] = result.ErrorCode ?? ErrorCodes.ValidationFailed }
        });
    }

    [HttpGet("me/twin/history")]
    [Authorize(Policy = "twin.student.read_own")]
    public async Task<IActionResult> GetStudentTwinHistory(
        [FromQuery] Guid? subjectId,
        [FromQuery] ulong? topicId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var traceId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _getStudentTwinHistoryUseCase.ExecuteAsync(subjectId, topicId, from, to, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new TwinHistoryResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = traceId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
                Status = StatusCodes.Status404NotFound,
                Title = "Không tìm thấy dữ liệu.",
                Detail = result.ErrorMessage ?? "Không tìm thấy lịch sử Digital Twin.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = ErrorCodes.ResourceNotFound }
            });
        }

        if (result.ErrorCode == ErrorCodes.ForbiddenResource)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
                Status = StatusCodes.Status403Forbidden,
                Title = "Không có quyền truy cập.",
                Detail = result.ErrorMessage ?? "Bạn không có quyền truy cập lịch sử Digital Twin này.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = ErrorCodes.ForbiddenResource }
            });
        }

        return BadRequest(new ProblemDetails
        {
            Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Dữ liệu không hợp lệ.",
            Detail = result.ErrorMessage ?? "Yêu cầu không hợp lệ.",
            Instance = HttpContext.Request.Path,
            Extensions = { ["traceId"] = traceId, ["errorCode"] = result.ErrorCode ?? ErrorCodes.ValidationFailed }
        });
    }
}
