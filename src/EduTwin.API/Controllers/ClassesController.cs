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

    public ClassesController(IListClassesUseCase listClassesUseCase)
    {
        _listClassesUseCase = listClassesUseCase;
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
                    TotalItems = result.TotalItems
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
}
