using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface IListPermissionsUseCase
{
    Task<ListPermissionsResult> ExecuteAsync(
        PermissionListQuery query,
        CancellationToken cancellationToken = default);
}
