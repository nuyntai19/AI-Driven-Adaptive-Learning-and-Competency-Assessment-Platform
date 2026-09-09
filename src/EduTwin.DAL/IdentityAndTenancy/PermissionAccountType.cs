using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.DAL.IdentityAndTenancy;

public sealed class PermissionAccountType
{
    public Guid PermissionId { get; set; }
    public UserRole AccountType { get; set; }
    public DateTime CreatedAt { get; set; }

    public Permission Permission { get; set; } = null!;
}
