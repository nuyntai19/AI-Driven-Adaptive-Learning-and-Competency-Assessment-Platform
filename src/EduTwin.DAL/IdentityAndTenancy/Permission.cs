using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.DAL.IdentityAndTenancy;

public sealed class Permission
{
    public Guid PermissionId { get; set; }
    public string PermissionCode { get; set; } = null!;
    public string ModuleName { get; set; } = null!;
    public string ResourceName { get; set; } = null!;
    public string ActionName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public bool IsSensitive { get; set; }
    public bool IsDelegable { get; set; }
    public PermissionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<PermissionAccountType> AllowedAccountTypes { get; set; } = [];
}
