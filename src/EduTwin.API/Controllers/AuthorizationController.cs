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
    IListAuthorizationRolesUseCase listRolesUseCase,
    IGetAuthorizationRoleUseCase getRoleUseCase,
    ICreateAuthorizationRoleUseCase createRoleUseCase,
    IUpdateAuthorizationRoleUseCase updateRoleUseCase,
    IReplaceRolePermissionsUseCase replaceRolePermissionsUseCase,
    IGetUserAuthorizationUseCase getUserAuthorizationUseCase,
    IReplaceUserRolesUseCase replaceUserRolesUseCase,
    IListAuthorizationAuditUseCase listAuditUseCase,
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

    [HttpGet("roles")]
    [Authorize(Policy = "authorization.roles.read")]
    public async Task<IActionResult> GetRoles(
        [FromQuery] AuthorizationRoleListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await listRolesUseCase.ExecuteAsync(query, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapRoleFailure(result.ErrorCode!);
        }

        return Ok(new AuthorizationRoleListResponse
        {
            Data = result.Data,
            Meta = new PagedMetaDto
            {
                Page = query.Page,
                PageSize = query.PageSize,
                TotalItems = result.TotalItems,
                TotalPages = result.TotalPages,
                TraceId = CurrentTraceId(),
                Timestamp = timeProvider.GetUtcNow().UtcDateTime
            }
        });
    }

    [HttpGet("roles/{roleId:guid}")]
    [Authorize(Policy = "authorization.roles.read")]
    public async Task<IActionResult> GetRole(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var result = await getRoleUseCase.ExecuteAsync(roleId, cancellationToken);
        return result.IsSuccess
            ? Ok(RoleResponse(result.Data!))
            : MapRoleFailure(result.ErrorCode!);
    }

    [HttpPost("roles")]
    [Authorize(Policy = "authorization.roles.create")]
    public async Task<IActionResult> CreateRole(
        [FromBody] CreateAuthorizationRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await createRoleUseCase.ExecuteAsync(
            request,
            CurrentTraceId(),
            cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(
                nameof(GetRole),
                new { roleId = result.Data!.RoleId },
                RoleResponse(result.Data))
            : MapRoleFailure(result.ErrorCode!);
    }

    [HttpPatch("roles/{roleId:guid}")]
    [Authorize(Policy = "authorization.roles.update")]
    public async Task<IActionResult> UpdateRole(
        Guid roleId,
        [FromBody] UpdateAuthorizationRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await updateRoleUseCase.ExecuteAsync(
            roleId,
            request,
            CurrentTraceId(),
            cancellationToken);
        return result.IsSuccess
            ? Ok(RoleResponse(result.Data!))
            : MapRoleFailure(result.ErrorCode!);
    }

    [HttpPut("roles/{roleId:guid}/permissions")]
    [Authorize(Policy = "authorization.roles.manage_permissions")]
    public async Task<IActionResult> ReplaceRolePermissions(
        Guid roleId,
        [FromBody] ReplaceRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await replaceRolePermissionsUseCase.ExecuteAsync(
            roleId,
            request,
            CurrentTraceId(),
            cancellationToken);
        return result.IsSuccess
            ? Ok(RoleResponse(result.Data!))
            : MapRoleFailure(result.ErrorCode!);
    }

    [HttpGet("users/{userId:guid}/roles")]
    [Authorize(Policy = "authorization.user_roles.read")]
    public async Task<IActionResult> GetUserRoles(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await getUserAuthorizationUseCase.ExecuteAsync(
            userId,
            cancellationToken);
        return result.IsSuccess
            ? Ok(UserAuthorizationResponse(result.Data!))
            : MapRoleFailure(result.ErrorCode!);
    }

    [HttpPut("users/{userId:guid}/roles")]
    [Authorize(Policy = "authorization.user_roles.assign")]
    public async Task<IActionResult> ReplaceUserRoles(
        Guid userId,
        [FromBody] ReplaceUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await replaceUserRolesUseCase.ExecuteAsync(
            userId,
            request,
            CurrentTraceId(),
            cancellationToken);
        return result.IsSuccess
            ? Ok(UserAuthorizationResponse(result.Data!))
            : MapRoleFailure(result.ErrorCode!);
    }

    [HttpGet("audit")]
    [Authorize(Policy = "authorization.audit.read")]
    public async Task<IActionResult> GetAudit(
        [FromQuery] AuthorizationAuditQuery query,
        CancellationToken cancellationToken)
    {
        var result = await listAuditUseCase.ExecuteAsync(query, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapRoleFailure(result.ErrorCode!);
        }

        return Ok(new AuthorizationAuditResponse
        {
            Data = result.Data,
            Meta = new PagedMetaDto
            {
                Page = query.Page,
                PageSize = query.PageSize,
                TotalItems = result.TotalItems,
                TotalPages = result.TotalPages,
                TraceId = CurrentTraceId(),
                Timestamp = timeProvider.GetUtcNow().UtcDateTime
            }
        });
    }

    private AuthorizationRoleResponse RoleResponse(AuthorizationRoleDto role) => new()
    {
        Data = role,
        Meta = new MetaDto
        {
            TraceId = CurrentTraceId(),
            Timestamp = timeProvider.GetUtcNow().UtcDateTime
        }
    };

    private UserAuthorizationResponse UserAuthorizationResponse(
        UserAuthorizationDto authorization) => new()
    {
        Data = authorization,
        Meta = new MetaDto
        {
            TraceId = CurrentTraceId(),
            Timestamp = timeProvider.GetUtcNow().UtcDateTime
        }
    };

    private IActionResult MapRoleFailure(string errorCode)
    {
        var (status, slug, title, detail) = errorCode switch
        {
            ErrorCodes.ValidationFailed =>
                (400, "validation", "Dữ liệu không hợp lệ", "Role request không hợp lệ."),
            ErrorCodes.ResourceNotFound =>
                (404, "resource-not-found", "Không tìm thấy dữ liệu", "Không tìm thấy role trong trung tâm hiện tại."),
            ErrorCodes.AuthPermissionRequired =>
                (403, "permission-required", "Không đủ quyền", "Thao tác yêu cầu permission bổ sung."),
            ErrorCodes.AuthPrivilegeEscalation =>
                (403, "privilege-escalation", "Không thể nâng đặc quyền", "Permission set vượt quá quyền được phép của người thao tác."),
            ErrorCodes.RoleAccountTypeMismatch =>
                (400, "role-account-type-mismatch", "Loại tài khoản không tương thích", "Permission không tương thích với account type của role."),
            ErrorCodes.DuplicateResource =>
                (409, "duplicate-resource", "Dữ liệu đã tồn tại", "Role code đã tồn tại trong trung tâm."),
            ErrorCodes.ConcurrencyConflict =>
                (409, "concurrency-conflict", "Xung đột cập nhật", "Role đã được cập nhật bởi request khác."),
            ErrorCodes.LastTenantAdmin =>
                (409, "last-tenant-admin", "Phải giữ quản trị viên cuối", "Thao tác sẽ làm trung tâm không còn quản trị viên hợp lệ."),
            ErrorCodes.InvalidStateTransition =>
                (409, "invalid-state-transition", "Trạng thái không hợp lệ", "Không thể archive system role."),
            _ => throw new InvalidOperationException($"Unexpected error code: {errorCode}")
        };
        return Problem(
            type: $"https://edutwin.local/problems/{slug}",
            title: title,
            statusCode: status,
            detail: detail,
            instance: HttpContext.Request.Path,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = errorCode,
                ["traceId"] = CurrentTraceId()
            });
    }

    private string CurrentTraceId() =>
        Activity.Current?.Id ?? HttpContext.TraceIdentifier;
}
