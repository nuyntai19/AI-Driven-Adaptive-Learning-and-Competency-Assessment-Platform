using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface IGetUserAuthorizationUseCase
{
    Task<UserAuthorizationResult> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface IReplaceUserRolesUseCase
{
    Task<UserAuthorizationResult> ExecuteAsync(
        Guid userId,
        ReplaceUserRolesRequest request,
        string traceId,
        CancellationToken cancellationToken = default);
}

public interface IListAuthorizationAuditUseCase
{
    Task<ListAuthorizationAuditResult> ExecuteAsync(
        AuthorizationAuditQuery query,
        CancellationToken cancellationToken = default);
}
