using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed record AuthorizationSnapshot(
    IReadOnlyList<AuthorizationRoleSummaryDto> Roles,
    IReadOnlyList<string> Permissions);

public interface IAuthorizationSnapshotReader
{
    Task<AuthorizationSnapshot> ReadForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
