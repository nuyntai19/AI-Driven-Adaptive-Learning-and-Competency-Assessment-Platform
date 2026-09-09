using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.IdentityAndTenancy;

public sealed class RolePermission : ITenantJoinEntity
{
    public Guid CenterId { get; set; }
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public UserRole AccountType { get; set; }
    public DateTime GrantedAt { get; set; }
    public Guid GrantedByUserId { get; set; }

    public AuthorizationRole Role { get; set; } = null!;
    public PermissionAccountType PermissionAccountType { get; set; } = null!;
    public User GrantedByUser { get; set; } = null!;
}
