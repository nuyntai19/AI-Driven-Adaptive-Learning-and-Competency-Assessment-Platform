using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class PermissionListQuery
{
    [StringLength(64)]
    public string? Module { get; set; }

    [EnumDataType(typeof(UserRole))]
    public UserRole? AccountType { get; set; }

    [EnumDataType(typeof(PermissionStatus))]
    public PermissionStatus? Status { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
