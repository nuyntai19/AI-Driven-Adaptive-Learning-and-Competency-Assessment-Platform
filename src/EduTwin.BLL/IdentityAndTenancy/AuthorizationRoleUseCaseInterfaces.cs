using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface IListAuthorizationRolesUseCase
{
    Task<ListAuthorizationRolesResult> ExecuteAsync(
        AuthorizationRoleListQuery query,
        CancellationToken cancellationToken = default);
}

public interface IGetAuthorizationRoleUseCase
{
    Task<AuthorizationRoleResult> ExecuteAsync(
        Guid roleId,
        CancellationToken cancellationToken = default);
}

public interface ICreateAuthorizationRoleUseCase
{
    Task<AuthorizationRoleResult> ExecuteAsync(
        CreateAuthorizationRoleRequest request,
        string traceId,
        CancellationToken cancellationToken = default);
}

public interface IUpdateAuthorizationRoleUseCase
{
    Task<AuthorizationRoleResult> ExecuteAsync(
        Guid roleId,
        UpdateAuthorizationRoleRequest request,
        string traceId,
        CancellationToken cancellationToken = default);
}

public interface IReplaceRolePermissionsUseCase
{
    Task<AuthorizationRoleResult> ExecuteAsync(
        Guid roleId,
        ReplaceRolePermissionsRequest request,
        string traceId,
        CancellationToken cancellationToken = default);
}
