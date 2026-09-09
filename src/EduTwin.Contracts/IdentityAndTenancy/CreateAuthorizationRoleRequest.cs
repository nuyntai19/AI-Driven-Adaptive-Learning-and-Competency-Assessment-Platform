using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class CreateAuthorizationRoleRequest
{
    [Required]
    [StringLength(64)]
    [RegularExpression("^[A-Z][A-Z0-9_]*$")]
    public string RoleCode { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string RoleName { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(UserRole))]
    public UserRole? AccountType { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}
