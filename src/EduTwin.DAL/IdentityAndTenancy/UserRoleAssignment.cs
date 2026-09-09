using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.IdentityAndTenancy;

public sealed class UserRoleAssignment : ITenantJoinEntity, IHasRowVersion
{
    public Guid CenterId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public UserRole AccountType { get; set; }
    public UserRoleAssignmentStatus Status { get; set; }
    public DateTime AssignedAt { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }
    public string? RevokeReason { get; set; }
    public ulong RowVersion { get; set; }

    public User User { get; set; } = null!;
    public AuthorizationRole Role { get; set; } = null!;
    public User AssignedByUser { get; set; } = null!;
    public User? RevokedByUser { get; set; }
}
