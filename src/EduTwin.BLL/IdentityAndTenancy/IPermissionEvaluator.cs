namespace EduTwin.BLL.IdentityAndTenancy;

public interface IPermissionEvaluator
{
    Task<bool> HasPermissionAsync(
        string permissionCode,
        CancellationToken cancellationToken = default);
}
