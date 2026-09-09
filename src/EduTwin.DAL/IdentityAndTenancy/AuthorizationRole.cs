using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.IdentityAndTenancy;

public sealed class AuthorizationRole : IMutableTenantAggregate
{
    public Guid RoleId { get; set; }
    public string RoleCode { get; set; } = null!;
    public string RoleName { get; set; } = null!;
    public UserRole AccountType { get; set; }
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public AuthorizationRoleStatus Status { get; set; }
    public Guid CenterId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; }

    public Organization.Center Center { get; set; } = null!;
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<UserRoleAssignment> UserAssignments { get; set; } = [];
}
