namespace EduTwin.BLL.IdentityAndTenancy;

public interface ITenantAdministratorGuard
{
    Task<bool> HasAdministratorAfterAsync(
        Guid? changedRoleId = null,
        bool? changedRoleActive = null,
        IReadOnlyCollection<Guid>? replacementPermissionIds = null,
        Guid? changedUserId = null,
        IReadOnlyCollection<Guid>? replacementUserRoleIds = null,
        CancellationToken cancellationToken = default);
}
