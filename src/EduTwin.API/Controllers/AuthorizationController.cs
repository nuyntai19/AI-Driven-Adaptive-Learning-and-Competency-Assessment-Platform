using System.Diagnostics;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/authorization")]
[Authorize]
public sealed class AuthorizationController(
    IListPermissionsUseCase listPermissionsUseCase,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet("permissions")]
    [Authorize(Policy = "authorization.permissions.read")]
    public async Task<IActionResult> GetPermissions(
        [FromQuery] PermissionListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await listPermissionsUseCase.ExecuteAsync(
            query,
            cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(new PermissionListResponse
            {
                Data = result.Data,
                Meta = new PagedMetaDto
                {
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalItems = result.TotalItems,
                    TotalPages = result.TotalPages,
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.ValidationFailed)
        {
            return Problem(
                type: "https://edutwin.local/problems/validation",
                title: "Dữ liệu không hợp lệ",
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Bộ lọc hoặc phân trang permission không hợp lệ.",
                instance: HttpContext.Request.Path,
                extensions: new Dictionary<string, object?>
                {
                    ["errorCode"] = ErrorCodes.ValidationFailed
                });
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return Problem(
                type: "https://edutwin.local/problems/resource-not-found",
                title: "Không tìm thấy dữ liệu",
                statusCode: StatusCodes.Status404NotFound,
                detail: "Không tìm thấy tenant hoặc người dùng hiện tại.",
                instance: HttpContext.Request.Path,
                extensions: new Dictionary<string, object?>
                {
                    ["errorCode"] = ErrorCodes.ResourceNotFound
                });
        }

        throw new InvalidOperationException(
            $"Unexpected error code: {result.ErrorCode}");
    }
}
