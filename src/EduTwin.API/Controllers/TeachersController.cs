using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/teachers")]
[Authorize]
public class TeachersController : ControllerBase
{
    private readonly IListTeachersUseCase _listTeachersUseCase;
    private readonly IGetTeacherUseCase _getTeacherUseCase;
    private readonly TimeProvider _timeProvider;

    public TeachersController(
        IListTeachersUseCase listTeachersUseCase,
        IGetTeacherUseCase getTeacherUseCase,
        TimeProvider timeProvider)
    {
        _listTeachersUseCase = listTeachersUseCase;
        _getTeacherUseCase = getTeacherUseCase;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CenterManagerOnly)]
    public async Task<IActionResult> GetTeachers([FromQuery] TeacherListQuery query, CancellationToken cancellationToken)
    {
        var result = await _listTeachersUseCase.ExecuteAsync(query, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new TeacherListResponse
            {
                Data = result.Data ?? new System.Collections.Generic.List<TeacherDto>(),
                Meta = new PagedMetaDto
                {
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalItems = result.TotalItems,
                    TotalPages = result.TotalPages,
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
                Detail = "Dữ liệu tìm kiếm hoặc phân trang không hợp lệ.",
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
                Detail = "Tài khoản hoặc trung tâm không tồn tại.",
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

    [HttpGet("{teacherId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    public async Task<IActionResult> GetTeacher([FromRoute] Guid teacherId, CancellationToken cancellationToken)
    {
        var result = await _getTeacherUseCase.ExecuteAsync(teacherId, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }

        if (result.ErrorCode == ErrorCodes.ForbiddenResource)
        {
            return StatusCode(403, new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.3",
                Title = "Không có quyền truy cập",
                Status = 403,
                Detail = "Bạn không có quyền xem thông tin tài khoản này.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.ForbiddenResource
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
                Detail = "Giáo viên không tồn tại hoặc không thuộc quyền quản lý của bạn.",
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
