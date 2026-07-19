using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class ClassesController : ControllerBase
{
    private readonly IListClassesUseCase _listClassesUseCase;
    private readonly IGetClassUseCase _getClassUseCase;
    private readonly TimeProvider _timeProvider;

    public ClassesController(IListClassesUseCase listClassesUseCase, IGetClassUseCase getClassUseCase, TimeProvider timeProvider)
    {
        _listClassesUseCase = listClassesUseCase;
        _getClassUseCase = getClassUseCase;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    public async Task<IActionResult> ListClasses([FromQuery] ClassListQuery query, CancellationToken cancellationToken)
    {
        var result = await _listClassesUseCase.ExecuteAsync(query, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new ClassListResponse
            {
                Data = result.Data!,
                Meta = new PagedMetaDto
                {
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalPages = result.TotalPages,
                    TotalItems = result.TotalItems,
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
                Detail = "Tài nguyên bạn yêu cầu không tồn tại hoặc đã bị xóa.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode }
            });
        }

        throw new System.InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }

    [HttpGet("{classId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    public async Task<IActionResult> GetClass([FromRoute] Guid classId, CancellationToken cancellationToken)
    {
        var result = await _getClassUseCase.ExecuteAsync(classId, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new
            {
                Data = result.Data!,
                Meta = new EduTwin.Contracts.IdentityAndTenancy.MetaDto
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
                Detail = "Tài nguyên bạn yêu cầu không tồn tại hoặc đã bị xóa.",
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
                Title = "Không có quyền truy cập.",
                Detail = "Bạn không có quyền truy cập tài nguyên này.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode }
            });
        }

        throw new System.InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }
}
